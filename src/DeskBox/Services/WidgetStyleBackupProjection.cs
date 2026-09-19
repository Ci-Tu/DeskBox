using System.Text.Json;
using System.Text.Json.Nodes;
using DeskBox.Models;

namespace DeskBox.Services;

/// <summary>
/// Projects widget STYLE settings into a portable backup document and
/// patches them back onto a settings.json file (roadmap §10, "格子样式"
/// domain). Style syncs; layout does not — absolute positions, monitor
/// topology, capsule ordering/free placements and internal version
/// counters are deliberately absent from the whitelists below.
///
/// The projection works on the serialized JSON DOM, not the typed model:
/// settings.json is a flat facade schema, so whitelists are expressed in
/// wire names and any untouched content round-trips byte-for-byte.
/// </summary>
internal static class WidgetStyleBackupProjection
{
    internal const int DocumentSchemaVersion = 1;
    internal const string DocumentKind = "widget-style";

    /// <summary>
    /// Flat settings.json keys carrying widget-shell style/display
    /// preferences — the wire-name whitelist for both directions.
    /// Excluded on purpose: widgetCapsuleBarOrder (ordering = layout),
    /// widgetCapsuleFreePlacements (coordinates), widgetCompactSettingsVersion
    /// (internal schema counter), and every WidgetLayoutSettingsSlice key.
    /// </summary>
    // internal (not private) so the drift ratchet in
    // WidgetStyleProjectionContractTests can enumerate the whitelist.
    internal static readonly HashSet<string> ShellKeys = new(StringComparer.Ordinal)
    {
        "defaultWidgetWidth", "defaultWidgetHeight",
        "widgetOpacity", "widgetMaterialType", "widgetMaterialIntensity",
        "widgetForegroundMode", "widgetForegroundColor",
        "widgetBorderColorMode", "widgetBorderStyle",
        "widgetCornerPreference",
        "widgetAnimationEffect", "widgetAnimationSpeed",
        "widgetAnimationSlideDirection", "widgetAnimationEasingIntensity",
        "widgetLayerMode",
        "keepWidgetsVisibleOnShowDesktop",
        "displayWidgetChromeMode", "interactiveWidgetChromeMode",
        "widgetCollapseBehavior",
        // JsonPropertyName("widgetCapsuleModeEnabled") on the facade — the
        // wire name differs from the slice property name.
        "widgetCapsuleModeEnabled",
        "widgetCompactWidthMode", "widgetCompactExpansionDirection",
        "widgetCapsuleArrangementMode",
        "widgetCapsuleBarSpacing", "widgetCapsuleBarPlacement",
        "widgetCapsuleBarDirection",
        "widgetCollapsedStyle", "widgetCompactContentMode",
        "widgetCompactHideSensitiveContent",
        "widgetCompactAnimationEffect", "widgetCompactAnimationDurationMs",
        "widgetCompactExpandDelayMs", "widgetCompactCollapseDelayMs",
        "widgetCompactMediaCornerMode",
        "widgetTitleIconMode", "showHoverButtons", "widgetHoverButtonActions",
        "resizeSnapEnabled", "widgetSnapSpacing", "focusClickedWidgetOnRaise",
        "iconSize", "textSize", "layoutDensity",
        "layoutDensityScale", "horizontalSpacingScale", "verticalSpacingScale"
    };

    /// <summary>
    /// Per-widget style fields whitelisted out of each WidgetConfig element:
    /// display/title/sort preferences only. Geometry (x, y, width, height,
    /// position*, compactPlacement), file bindings (mappedFolderPath, items,
    /// fileAddedAt*), state (isVisible, isDisabled, *Locked) and metadata
    /// never leave the device.
    /// </summary>
    internal static readonly HashSet<string> WidgetKeys = new(StringComparer.Ordinal)
    {
        "name", "isDefaultTitle", "viewMode", "iconSizeOverride",
        "isCollapsed", "compactWidth", "sortMode", "sortDescending"
    };

    internal sealed record ApplyResult(
        bool Applied,
        int ShellFieldsPatched,
        int WidgetsPatched,
        string? SkippedReason);

    /// <summary>
    /// Builds the widget-style document from the live settings object.
    /// Returns the UTF-8 JSON payload stored as widget-style.json inside a
    /// scoped backup archive.
    /// </summary>
    internal static byte[] Serialize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        JsonObject settingsDom = JsonSerializer
            .SerializeToNode(settings, SettingsJsonContext.Default.AppSettings)!
            .AsObject();

        var shell = new JsonObject();
        foreach (string key in ShellKeys)
        {
            if (settingsDom.TryGetPropertyValue(key, out JsonNode? value) && value is not null)
            {
                shell[key] = value.DeepClone();
            }
        }

        var widgets = new JsonObject();
        if (settingsDom.TryGetPropertyValue("widgets", out JsonNode? widgetsNode) &&
            widgetsNode is JsonArray widgetsArray)
        {
            foreach (JsonNode? node in widgetsArray)
            {
                if (node is not JsonObject element ||
                    element["id"] is not JsonValue idValue ||
                    !idValue.TryGetValue(out string? id) ||
                    string.IsNullOrEmpty(id))
                {
                    continue;
                }

                var style = new JsonObject();
                foreach (string key in WidgetKeys)
                {
                    if (element.TryGetPropertyValue(key, out JsonNode? value))
                    {
                        style[key] = value?.DeepClone();
                    }
                }

                // widgetKind is carried for verification only — it is never
                // applied; a kind mismatch skips the widget defensively.
                if (element.TryGetPropertyValue("widgetKind", out JsonNode? kindNode))
                {
                    style["widgetKind"] = kindNode?.DeepClone();
                }

                widgets[id] = style;
            }
        }

        var document = new JsonObject
        {
            ["schemaVersion"] = DocumentSchemaVersion,
            ["kind"] = DocumentKind,
            ["createdAtUtc"] = DateTimeOffset.UtcNow,
            ["sourceDeviceId"] = DeviceIdentity.Id,
            ["shell"] = shell,
            ["widgets"] = widgets
        };
        return JsonSerializer.SerializeToUtf8Bytes(document);
    }

    /// <summary>
    /// Patches the style document onto the live data files, atomically via
    /// the same resilient-write path the settings store itself uses. Only
    /// whitelisted keys are written; widgets are matched by id and verified
    /// by widgetKind before any field is touched. Shell keys always land in
    /// settings.json; per-widget keys land wherever the device layout
    /// currently lives — widget-layout.json once it exists, else the legacy
    /// settings.json widgets array (pre-migration profiles, which the layout
    /// store then adopts).
    /// </summary>
    internal static async Task<ApplyResult> ApplyAsync(
        byte[] documentBytes,
        string settingsPath,
        string layoutPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);

        JsonObject document;
        try
        {
            document = JsonNode.Parse(documentBytes)?.AsObject()
                       ?? throw new InvalidDataException("The widget-style document is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The widget-style document is invalid.", ex);
        }

        if (document["schemaVersion"] is not JsonValue versionValue ||
            !versionValue.TryGetValue(out int version) ||
            version > DocumentSchemaVersion)
        {
            throw new InvalidDataException("The widget-style document schema is not supported.");
        }

        if (document["kind"] is not JsonValue kindValue ||
            !kindValue.TryGetValue(out string? kind) ||
            !string.Equals(kind, DocumentKind, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The widget-style document kind is invalid.");
        }

        if (!File.Exists(settingsPath))
        {
            return new ApplyResult(false, 0, 0, "settings.json does not exist");
        }

        // Keep the original bytes: the settings and layout commits below are
        // two independent stores and cannot be atomic, so a layout-commit
        // failure rolls the settings file back to exactly this content.
        string originalSettingsJson = await File.ReadAllTextAsync(
            settingsPath, cancellationToken);
        JsonObject settingsDom = JsonNode.Parse(originalSettingsJson)?.AsObject()
            ?? throw new InvalidDataException("settings.json is empty.");

        int shellPatched = 0;
        if (document["shell"] is JsonObject shellDoc)
        {
            foreach ((string key, JsonNode? value) in shellDoc)
            {
                if (!ShellKeys.Contains(key))
                {
                    continue;
                }

                settingsDom[key] = value?.DeepClone();
                shellPatched++;
            }
        }

        // The widgets array lives in widget-layout.json once the device store
        // exists; before adoption it is still a settings.json key.
        JsonObject? layoutDom = null;
        JsonArray? widgetsArray = null;
        bool widgetsInLayoutFile = File.Exists(layoutPath);
        if (widgetsInLayoutFile)
        {
            await using (var input = new FileStream(
                             layoutPath,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             bufferSize: 81920,
                             useAsync: true))
            {
                layoutDom = (await JsonNode.ParseAsync(
                        input,
                        cancellationToken: cancellationToken))?.AsObject()
                    ?? throw new InvalidDataException("widget-layout.json is empty.");
            }

            widgetsArray = layoutDom["layout"]?["widgets"] as JsonArray;
        }
        else if (settingsDom.TryGetPropertyValue("widgets", out JsonNode? widgetsNode))
        {
            widgetsArray = widgetsNode as JsonArray;
        }

        int widgetsPatched = 0;
        if (document["widgets"] is JsonObject widgetsDoc &&
            widgetsArray is not null)
        {
            foreach (JsonNode? node in widgetsArray)
            {
                if (node is not JsonObject element ||
                    element["id"] is not JsonValue idValue ||
                    !idValue.TryGetValue(out string? id) ||
                    string.IsNullOrEmpty(id) ||
                    widgetsDoc[id] is not JsonObject style)
                {
                    continue;
                }

                string? docKind = style["widgetKind"] is JsonValue docKindValue &&
                                  docKindValue.TryGetValue(out string? dk) ? dk : null;
                string? localKind = element["widgetKind"] is JsonValue localKindValue &&
                                    localKindValue.TryGetValue(out string? lk) ? lk : null;
                if (!string.Equals(docKind, localKind, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (string key in WidgetKeys)
                {
                    if (style.TryGetPropertyValue(key, out JsonNode? value))
                    {
                        element[key] = value?.DeepClone();
                    }
                }

                widgetsPatched++;
            }
        }

        string patched = settingsDom.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        await ResilientJsonStore.SaveAsync(settingsPath, patched);
        if (widgetsInLayoutFile && layoutDom is not null)
        {
            string patchedLayout = layoutDom.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            try
            {
                await ResilientJsonStore.SaveAsync(layoutPath, patchedLayout);
            }
            catch
            {
                // The settings commit already landed; leaving it would apply
                // half the restore (new shell style, old widget styles).
                // Roll the settings file back to its pre-apply bytes —
                // best-effort, the original failure still propagates.
                try
                {
                    await ResilientJsonStore.SaveAsync(settingsPath, originalSettingsJson);
                }
                catch (Exception rollbackException)
                {
                    App.Log(
                        $"[WidgetStyleRestore] Settings rollback after " +
                        $"layout-commit failure also failed: {rollbackException}");
                }

                throw;
            }
        }
        return new ApplyResult(true, shellPatched, widgetsPatched, null);
    }
}
