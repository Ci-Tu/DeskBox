using DeskBox.Models;
using DeskBox.Services;

namespace DeskBox.ViewModels;

public partial class SettingsViewModel
{
    public IReadOnlyList<SettingsOption> AvailableLanguageOptions =>
        CreateSelectionOptions(AvailableLanguages, AvailableLanguageDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableQuickCaptureDefaultViewOptions =>
        CreateSelectionOptions(AvailableQuickCaptureDefaultViews, AvailableQuickCaptureDefaultViewDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableQuickCaptureTabStyleOptions =>
        CreateSelectionOptions(AvailableWidgetTabStyles, AvailableQuickCaptureTabStyleDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableItemPreviewLineCountOptions =>
        CreateSelectionOptions(AvailableItemPreviewLineCounts, AvailableItemPreviewLineCountDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableEditorEnterBehaviorOptions =>
        CreateSelectionOptions(AvailableEditorEnterBehaviors, AvailableEditorEnterBehaviorDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableQuickCaptureFormatOptions =>
        CreateSelectionOptions(AvailableQuickCaptureFormats, AvailableQuickCaptureFormatDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableQuickCaptureWideLayoutOptions =>
        CreateSelectionOptions(AvailableQuickCaptureWideLayouts, AvailableQuickCaptureWideLayoutDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableQuickCaptureWideOpenModeOptions =>
        CreateSelectionOptions(AvailableQuickCaptureWideOpenModes, AvailableQuickCaptureWideOpenModeDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableTodoNewTaskPositionOptions =>
        CreateSelectionOptions(AvailableTodoNewTaskPositions, AvailableTodoNewTaskPositionDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableTodoLayoutModeOptions =>
        CreateSelectionOptions(AvailableTodoLayoutModes, AvailableTodoLayoutModeDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableAttachmentStorageModeOptions =>
        CreateSelectionOptions(AvailableAttachmentStorageModes, AvailableAttachmentStorageModeDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableTodoDefaultFilterOptions =>
        CreateSelectionOptions(AvailableTodoDefaultFilters, AvailableTodoDefaultFilterDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableTodoTabStyleOptions =>
        CreateSelectionOptions(AvailableWidgetTabStyles, AvailableTodoTabStyleDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableTodoReminderOffsetOptions =>
        CreateSelectionOptions(AvailableTodoReminderOffsetMinutes, AvailableTodoReminderOffsetDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableWeatherTemperatureUnitOptions =>
        CreateSelectionOptions(AvailableWeatherTemperatureUnits, AvailableWeatherTemperatureUnitDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableWeatherWindSpeedUnitOptions =>
        CreateSelectionOptions(AvailableWeatherWindSpeedUnits, AvailableWeatherWindSpeedUnitDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableWeatherDefaultViewOptions =>
        CreateSelectionOptions(AvailableWeatherDefaultViews, AvailableWeatherDefaultViewDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableWeatherSkinOptions =>
        CreateSelectionOptions(AvailableWeatherSkins, AvailableWeatherSkinDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableWeatherDataSourceOptions =>
        CreateSelectionOptions(AvailableWeatherDataSources, AvailableWeatherDataSourceDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableWeatherRefreshIntervalOptions =>
        CreateSelectionOptions(AvailableWeatherRefreshIntervals, AvailableWeatherRefreshIntervalDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableFileStackGroupByOptions =>
        CreateSelectionOptions(AvailableFileStackGroupBys, AvailableFileStackGroupByDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableFileStackThresholdOptions =>
        CreateSelectionOptions(AvailableFileStackThresholds, AvailableFileStackThresholdDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableFileStackOrderByOptions =>
        CreateSelectionOptions(AvailableFileStackOrderBys, AvailableFileStackOrderByDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableFileStackUnmatchedBehaviorOptions =>
        CreateSelectionOptions(AvailableFileStackUnmatchedBehaviors, AvailableFileStackUnmatchedBehaviorDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableAutomaticBackupIntervalOptions =>
        CreateSelectionOptions(
            AvailableAutomaticBackupIntervals,
            AvailableAutomaticBackupIntervalDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableAutomaticBackupRetentionOptions =>
        CreateSelectionOptions(
            AvailableAutomaticBackupRetentionCounts,
            AvailableAutomaticBackupRetentionDisplayNames);

    internal static IReadOnlyList<SettingsOption> CreateSelectionOptions<T>(
        IReadOnlyList<T> values,
        IReadOnlyList<string> displayNames)
    {
        if (values.Count != displayNames.Count)
        {
            throw new InvalidOperationException("Setting option values and display names must have the same length.");
        }

        var options = new SettingsOption[values.Count];
        for (int index = 0; index < values.Count; index++)
        {
            options[index] = new SettingsOption(values[index]!, displayNames[index]);
        }

        return options;
    }

    /// <summary>
    /// A collection expression whose target is IReadOnlyList&lt;T&gt; compiles to the
    /// hidden &lt;&gt;z__ReadOnlyArray type, which CsWinRT cannot marshal across the
    /// WinRT ABI in Native AOT builds, leaving every {Binding} ItemsSource built
    /// this way empty. Routing the literal through an array parameter produces a
    /// real SettingsOption[] that projects correctly in JIT and AOT alike.
    /// </summary>
    internal static IReadOnlyList<SettingsOption> WrapOptions(SettingsOption[] options) => options;

    private void NotifySelectionOptionsChanged()
    {
        OnPropertyChanged(nameof(AvailableFileWidgetFolderOpenBehaviorOptions));
        OnPropertyChanged(nameof(AvailableFileWidgetFolderOpenBehaviorOptionItems));
        OnPropertyChanged(nameof(AvailableWeatherLocationModeOptions));
        OnPropertyChanged(nameof(AvailableLanguageOptions));
        OnPropertyChanged(nameof(AvailableQuickCaptureDefaultViewOptions));
        OnPropertyChanged(nameof(AvailableQuickCaptureTabStyleOptions));
        OnPropertyChanged(nameof(AvailableItemPreviewLineCountOptions));
        OnPropertyChanged(nameof(AvailableEditorEnterBehaviorOptions));
        OnPropertyChanged(nameof(AvailableQuickCaptureFormatOptions));
        OnPropertyChanged(nameof(AvailableQuickCaptureWideLayoutOptions));
        OnPropertyChanged(nameof(AvailableQuickCaptureWideOpenModeOptions));
        OnPropertyChanged(nameof(AvailableTodoNewTaskPositionOptions));
        OnPropertyChanged(nameof(AvailableTodoLayoutModeOptions));
        OnPropertyChanged(nameof(AvailableAttachmentStorageModeOptions));
        OnPropertyChanged(nameof(AvailableTodoDefaultFilterOptions));
        OnPropertyChanged(nameof(AvailableTodoTabStyleOptions));
        OnPropertyChanged(nameof(AvailableTodoReminderOffsetOptions));
        OnPropertyChanged(nameof(AvailableWeatherTemperatureUnitOptions));
        OnPropertyChanged(nameof(AvailableWeatherWindSpeedUnitOptions));
        OnPropertyChanged(nameof(AvailableWeatherDefaultViewOptions));
        OnPropertyChanged(nameof(AvailableWeatherSkinOptions));
        OnPropertyChanged(nameof(AvailableWeatherDataSourceOptions));
        OnPropertyChanged(nameof(AvailableWeatherRefreshIntervalOptions));
        OnPropertyChanged(nameof(AvailableFileStackGroupByOptions));
        OnPropertyChanged(nameof(AvailableFileStackThresholdOptions));
        OnPropertyChanged(nameof(AvailableFileStackOrderByOptions));
        OnPropertyChanged(nameof(AvailableFileStackUnmatchedBehaviorOptions));
        OnPropertyChanged(nameof(AvailableAutomaticBackupIntervalOptions));
        OnPropertyChanged(nameof(AvailableAutomaticBackupRetentionOptions));
    }
}
