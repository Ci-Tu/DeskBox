using System.Text.Json;
using System.Text.Json.Serialization;
using DeskBox.Services;

namespace DeskBox.Core.Persistence;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(
    typeof(SyncStateDocument),
    TypeInfoPropertyName = "SyncStateDocument")]
internal sealed partial class SyncStateJsonContext : JsonSerializerContext
{
}

/// <summary>
/// data/sync/state.json — per-domain pull cursors, the account's epoch, and
/// the account summary (sync-protocol-contract §8). Device-domain protocol
/// state: never backed up, never synced.
/// </summary>
public sealed class SyncStateDocument
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Server-side user id once an account is linked; null when
    /// sync is unconfigured.</summary>
    public string? AccountId { get; set; }

    /// <summary>Per-domain pull state keyed by the wire domain name
    /// (todo-data / quick-capture-data / widget-style).</summary>
    public Dictionary<string, SyncDomainState> Domains { get; set; } = new();
}

public sealed class SyncDomainState
{
    /// <summary>Opaque per-domain pull cursor issued by the server (§4).</summary>
    public string Cursor { get; set; } = string.Empty;

    /// <summary>Account epoch last seen on pull; a mismatch switches the
    /// domain to full-snapshot replace (§4).</summary>
    public long Epoch { get; set; }

    public DateTimeOffset? LastSyncAtUtc { get; set; }
}

/// <summary>
/// File owner for <see cref="SyncStateDocument"/>. Dumb durable holder —
/// the engine (a later module) owns the semantics; this class owns atomic
/// reads/writes plus corruption recovery.
/// </summary>
public sealed class SyncStateStore
{
    private const int CurrentSchemaVersion = 1;

    private readonly string _statePath;
    private int _loadedSchemaVersion = CurrentSchemaVersion;

    public SyncStateStore(string? statePath = null)
    {
        _statePath = statePath ?? Path.Combine(
            DeskBoxDataPathService.Current.DataDirectory,
            "sync",
            "state.json");
    }

    public Task<SyncStateDocument> LoadAsync() =>
        ResilientJsonStore.LoadAsync(
            _statePath,
            json =>
            {
                // Fail closed on "parses but is not a document": `null` or a
                // record missing its required collections is corrupt state,
                // not an empty store — throwing routes the file through
                // quarantine and .bak recovery instead of silently starting
                // over with account id, cursors and epoch all dropped.
                SyncStateDocument? document = JsonSerializer.Deserialize(
                    json, SyncStateJsonContext.Default.SyncStateDocument);
                if (document is null || document.Domains is null)
                {
                    throw new InvalidDataException(
                        "sync state.json is structurally empty.");
                }

                _loadedSchemaVersion = document.SchemaVersion;
                return document;
            },
            static () => new SyncStateDocument(),
            "SyncState");

    public Task SaveAsync(SyncStateDocument document)
    {
        // Same read-only stance as WidgetLayoutStore.CanWrite: a file stamped
        // by a newer build is pristine to this one — overwriting it would
        // drop fields the newer schema introduced.
        if (_loadedSchemaVersion > CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"sync state.json schema {_loadedSchemaVersion} is newer than " +
                $"this build understands ({CurrentSchemaVersion}); " +
                "refusing to overwrite it.");
        }

        return ResilientJsonStore.SaveAsync(
            _statePath,
            JsonSerializer.SerializeToUtf8Bytes(
                document, SyncStateJsonContext.Default.SyncStateDocument));
    }

    /// <inheritdoc cref="WidgetLayoutStore.SaveCheckedAsync"/>
    public async Task<bool> SaveCheckedAsync(SyncStateDocument document)
    {
        try
        {
            await SaveAsync(document);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            App.Log($"[SyncState] Save failed: {ex}");
            return false;
        }
    }
}
