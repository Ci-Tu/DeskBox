using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using DeskBox.Models;
using DeskBox.Services;

namespace DeskBox.Sync;

/// <summary>
/// Local domain record → <see cref="SyncEnvelope"/> projection
/// (sync-protocol-contract §3.3). Pure transformation, no I/O beyond
/// hashing attachment bytes for blob references.
///
/// Path discipline: payload file paths are always collection-relative
/// (<c>attachments/report.pdf</c>, <c>images/&lt;hash&gt;.png</c>) — a local
/// absolute path never crosses the wire. Managed attachments additionally
/// earn a content-addressed blob reference; linked attachments keep only
/// their basename as a display stub (the record still renders, degraded).
/// </summary>
public static class SyncProjection
{
    /// <summary>Per-domain payload schema versions (envelope.schema_version,
    /// §3.1). Bump the domain's version only when its payload shape changes.</summary>
    public const int TodoSchemaVersion = 1;
    public const int QuickCaptureSchemaVersion = 1;
    public const int WidgetStyleSchemaVersion = 1;

    /// <summary>todo-data: one collection per widget — the collection id IS
    /// the widget id (§2.1), so independently created todo widgets on two
    /// devices never merge.</summary>
    public static async Task<SyncEnvelope> FromTodoItemAsync(
        TodoItem item,
        string widgetId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(widgetId);

        JsonObject payload = JsonSerializer
            .SerializeToNode(item, TodoJsonContext.Default.TodoItem)!
            .AsObject();

        List<SyncAttachmentRef> blobRefs = await ProjectAttachmentsAsync(
            payload["attachments"] as JsonArray,
            cancellationToken);

        return new SyncEnvelope
        {
            Domain = SyncDomains.TodoData,
            CollectionId = widgetId,
            EntityId = item.Id,
            SchemaVersion = TodoSchemaVersion,
            Deleted = item.IsDeleted,
            DeviceId = item.DeviceId ?? DeviceIdentity.Id,
            OperationId = Guid.NewGuid().ToString("N"),
            Payload = payload,
            Attachments = blobRefs
        };
    }

    /// <summary>quick-capture-data: singleton collection (§2.1) — items
    /// merge across devices by construction.</summary>
    public static async Task<SyncEnvelope> FromQuickCaptureItemAsync(
        QuickCaptureItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        JsonObject payload = JsonSerializer
            .SerializeToNode(item, QuickCaptureJsonContext.Default.QuickCaptureItem)!
            .AsObject();

        List<SyncAttachmentRef> blobRefs = await ProjectAttachmentsAsync(
            payload["attachments"] as JsonArray,
            cancellationToken);

        // The capture image lives under quick-capture/images/ and is already
        // content-hash named — it rides the blob channel like any managed
        // attachment, with an images/ collection-relative payload path.
        if (TryGetString(payload["imagePath"], out string? imagePath) &&
            !string.IsNullOrEmpty(imagePath))
        {
            string basename = Path.GetFileName(imagePath);
            payload["imagePath"] = string.IsNullOrEmpty(basename)
                ? string.Empty
                : $"images/{basename}";
            if (!string.IsNullOrEmpty(basename) && File.Exists(imagePath))
            {
                blobRefs.Add(await BlobRefAsync(basename, imagePath, cancellationToken));
            }
        }

        return new SyncEnvelope
        {
            Domain = SyncDomains.QuickCaptureData,
            CollectionId = SyncDomains.QuickCaptureCollection,
            EntityId = item.Id,
            SchemaVersion = QuickCaptureSchemaVersion,
            Deleted = item.IsDeleted,
            DeviceId = item.DeviceId ?? DeviceIdentity.Id,
            OperationId = Guid.NewGuid().ToString("N"),
            Payload = payload,
            Attachments = blobRefs
        };
    }

    /// <summary>widget-style: singleton collection; the "shell" entity
    /// carries every shell-whitelist key, each widget id carries its style
    /// patch. The whitelist is enforced by the same code path that builds
    /// backup style documents — there is no second list to drift (§3.3).</summary>
    public static IEnumerable<SyncEnvelope> FromWidgetStyle(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        JsonObject document = JsonNode
            .Parse(WidgetStyleBackupProjection.Serialize(settings))!
            .AsObject();

        if (document["shell"] is JsonObject shell)
        {
            yield return new SyncEnvelope
            {
                Domain = SyncDomains.WidgetStyle,
                CollectionId = SyncDomains.WidgetStyleCollection,
                EntityId = SyncDomains.ShellEntityId,
                SchemaVersion = WidgetStyleSchemaVersion,
                DeviceId = DeviceIdentity.Id,
                OperationId = Guid.NewGuid().ToString("N"),
                Payload = (JsonObject)shell.DeepClone()
            };
        }

        if (document["widgets"] is JsonObject widgets)
        {
            foreach ((string widgetId, JsonNode? style) in widgets)
            {
                if (style is not JsonObject styleObject)
                {
                    continue;
                }

                yield return new SyncEnvelope
                {
                    Domain = SyncDomains.WidgetStyle,
                    CollectionId = SyncDomains.WidgetStyleCollection,
                    EntityId = widgetId,
                    SchemaVersion = WidgetStyleSchemaVersion,
                    DeviceId = DeviceIdentity.Id,
                    OperationId = Guid.NewGuid().ToString("N"),
                    Payload = (JsonObject)styleObject.DeepClone()
                };
            }
        }
    }

    /// <summary>Tombstone envelope for a locally deleted entity (§4):
    /// deleted=true, no payload, no blob references.</summary>
    public static SyncEnvelope Tombstone(string domain, string collectionId, string entityId)
    {
        if (!SyncDomains.IsKnownDomain(domain))
        {
            throw new ArgumentException($"Unknown sync domain '{domain}'.", nameof(domain));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        return new SyncEnvelope
        {
            Domain = domain,
            CollectionId = collectionId,
            EntityId = entityId,
            Deleted = true,
            DeviceId = DeviceIdentity.Id,
            OperationId = Guid.NewGuid().ToString("N")
        };
    }

    /// <summary>Rewrites an attachments array in place and collects blob
    /// references for the files that exist locally.</summary>
    private static async Task<List<SyncAttachmentRef>> ProjectAttachmentsAsync(
        JsonArray? payloadAttachments,
        CancellationToken cancellationToken)
    {
        var blobRefs = new List<SyncAttachmentRef>();
        if (payloadAttachments is null)
        {
            return blobRefs;
        }

        foreach (JsonNode? node in payloadAttachments)
        {
            if (node is not JsonObject attachment ||
                !TryGetString(attachment["filePath"], out string? filePath) ||
                string.IsNullOrEmpty(filePath))
            {
                continue;
            }

            string basename = Path.GetFileName(filePath);
            bool managed = TryGetString(attachment["storageMode"], out string? mode) &&
                           string.Equals(mode, "managed", StringComparison.OrdinalIgnoreCase);
            if (managed)
            {
                attachment["filePath"] = $"attachments/{basename}";
                if (File.Exists(filePath))
                {
                    blobRefs.Add(await BlobRefAsync(basename, filePath, cancellationToken));
                }
            }
            else
            {
                // Linked attachment: the absolute path stays on this device;
                // the basename survives as a display-only stub (§3.3).
                attachment["filePath"] = basename;
            }
        }

        return blobRefs;
    }

    private static async Task<SyncAttachmentRef> BlobRefAsync(
        string name,
        string localPath,
        CancellationToken cancellationToken)
    {
        byte[] bytes = await File.ReadAllBytesAsync(localPath, cancellationToken);
        return new SyncAttachmentRef
        {
            Name = name,
            BlobId = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            Size = bytes.LongLength
        };
    }

    private static bool TryGetString(JsonNode? node, out string? value)
    {
        value = node is JsonValue jsonValue && jsonValue.TryGetValue(out string? s) ? s : null;
        return node is not null;
    }
}
