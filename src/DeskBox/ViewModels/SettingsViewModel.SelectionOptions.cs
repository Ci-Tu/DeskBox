using DeskBox.Models;
using DeskBox.Services;

namespace DeskBox.ViewModels;

public partial class SettingsViewModel
{
    public IReadOnlyList<SettingsOption> AvailableLanguageOptions =>
        CreateSelectionOptions(AvailableLanguages, AvailableLanguageDisplayNames);

    public IReadOnlyList<SettingsOption> AvailableAttachmentStorageModeOptions =>
        CreateSelectionOptions(AvailableAttachmentStorageModes, AvailableAttachmentStorageModeDisplayNames);

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
        OnPropertyChanged(nameof(AvailableWeatherLocationModeOptions));
        OnPropertyChanged(nameof(AvailableLanguageOptions));
        OnPropertyChanged(nameof(AvailableAttachmentStorageModeOptions));
        OnPropertyChanged(nameof(AvailableWeatherTemperatureUnitOptions));
        OnPropertyChanged(nameof(AvailableWeatherWindSpeedUnitOptions));
        OnPropertyChanged(nameof(AvailableWeatherDefaultViewOptions));
        OnPropertyChanged(nameof(AvailableWeatherSkinOptions));
        OnPropertyChanged(nameof(AvailableWeatherDataSourceOptions));
        OnPropertyChanged(nameof(AvailableWeatherRefreshIntervalOptions));
        OnPropertyChanged(nameof(AvailableAutomaticBackupIntervalOptions));
        OnPropertyChanged(nameof(AvailableAutomaticBackupRetentionOptions));
    }
}
