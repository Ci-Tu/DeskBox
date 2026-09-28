using DeskBox.Models;
using DeskBox.Services;

namespace DeskBox.ViewModels;

public partial class SettingsViewModel
{
    // The preview-line-count and enter-behavior option tables stay on the
    // shell because the Todo section still binds them; the Quick Capture
    // section owns its copies on its section editor (batch 46).
    private string[]? _cachedItemPreviewLineCountDisplayNames;
    private string[]? _cachedEditorEnterBehaviorDisplayNames;

    public int[] AvailableItemPreviewLineCounts { get; } =
        Enumerable.Range(
            SettingsService.MinItemPreviewLineCount,
            SettingsService.MaxItemPreviewLineCount - SettingsService.MinItemPreviewLineCount + 1)
        .ToArray();

    public string[] AvailableItemPreviewLineCountDisplayNames =>
        _cachedItemPreviewLineCountDisplayNames ??=
            AvailableItemPreviewLineCounts
                .Select(lineCount => lineCount == 1
                    ? _localizationService.T("Settings.ContentEditor.PreviewLines.Option.Single")
                    : _localizationService.Format(
                        "Settings.ContentEditor.PreviewLines.Option.Multiple",
                        lineCount))
                .ToArray();

    public string[] AvailableEditorEnterBehaviors { get; } =
    [
        SettingsService.EditorEnterBehaviorCtrlEnterSaves,
        SettingsService.EditorEnterBehaviorEnterSaves
    ];

    public string[] AvailableEditorEnterBehaviorDisplayNames =>
        _cachedEditorEnterBehaviorDisplayNames ??=
            AvailableEditorEnterBehaviors.Select(GetEditorEnterBehaviorDisplayName).ToArray();

    public int TodoItemPreviewLineCount
    {
        get => _todoSettings.PreviewLineCount;
        set
        {
            if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
            {
                return;
            }
            _todoSettings.PreviewLineCount = value;
        }
    }


    public string TodoEditorEnterBehavior
    {
        get => _todoSettings.EditorEnterBehavior;
        set
        {
            if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
            {
                return;
            }
            _todoSettings.EditorEnterBehavior = value;
        }
    }

    private void RefreshContentEditorLocalizedProperties()
    {
        _cachedItemPreviewLineCountDisplayNames = null;
        _cachedEditorEnterBehaviorDisplayNames = null;
        OnPropertyChanged(nameof(AvailableItemPreviewLineCountDisplayNames));
        OnPropertyChanged(nameof(AvailableEditorEnterBehaviorDisplayNames));
        OnPropertyChanged(nameof(TodoContentSummaryText));
    }

    private string GetEditorEnterBehaviorDisplayName(string behavior) =>
        SettingsService.NormalizeEditorEnterBehavior(behavior) ==
        SettingsService.EditorEnterBehaviorEnterSaves
            ? _localizationService.T("Settings.ContentEditor.EnterBehavior.EnterSaves")
            : _localizationService.T("Settings.ContentEditor.EnterBehavior.CtrlEnterSaves");
}
