using System.Globalization;
using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskBox.Helpers;
using DeskBox.Models;
using DeskBox.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DeskBox.ViewModels;

public partial class SettingsViewModel
{
private void OnLanguageChanged()
{
    RefreshLocalizedProperties();
    _musicSettings.RefreshLocalization();
    _interactionSettings.RefreshLocalization();
    _interactionSettings.UpdateHoverButtonActionsSummary(BuildHoverButtonActionsSummary());
    RefreshGlobalHotkeyState();
    // The managed-storage editor's option list re-localizes itself and
    // the quick-access card is re-pushed in the new language.
    _managedStorageSettings.RefreshLocalization();
    PushQuickAccessPresentation();
    // The appearance editor's option tables and value texts re-localize
    // themselves. The group-navigation and capsule editors rebuild their
    // option tables too; their pushed projections (existing groups, override
    // lists/summaries) are rebuilt by the notify calls below.
    _appearanceSettings.RefreshLocalization();
    _groupNavigationSettings.RefreshLocalization();
    _capsuleSettings.RefreshLocalization();
    RefreshWidgetGroupSettings();
}


    private void OnSettingsChanged()
    {
        if (App.UiDispatcherQueue is { } dispatcherQueue && !dispatcherQueue.HasThreadAccess)
        {
            dispatcherQueue.TryEnqueue(OnSettingsChanged);
            return;
        }

        ApplySettingsSnapshot();
    }

    private void ApplySettingsSnapshot()
    {
        var settings = _settingsService.Settings;
        bool wasRestoringDefaults = _isRestoringDefaults;

        _isApplyingSettingsSnapshot = true;
        _isRestoringDefaults = true;
        try
        {
            SelectedLanguage = LocalizationService.NormalizeLanguageSetting(settings.Language);
            AutoCheckForUpdates = settings.AutoCheckForUpdates;
            ShowHoverButtons = settings.ShowHoverButtons;
            ApplyHoverButtonActionSelection(settings.WidgetHoverButtonActions);

            IdleWorkingSetTrimEnabled = settings.IdleWorkingSetTrimEnabled;
            ImmediateHiddenWorkingSetTrimEnabled = settings.ImmediateHiddenWorkingSetTrimEnabled;
            QuiescenceWorkingSetTrimEnabled = settings.Performance.QuiescenceWorkingSetTrimEnabled;

            ApplyContentEditorSettingsSnapshot(settings);

            // The file-stack section (including its custom-rule collection)
            // and the file-widget overview's folder-open combo live on their
            // section editors now: re-project from the coordinator snapshots
            // instead of assigning shell facade properties.
            _fileStackSettings.SyncPresentation();
            _featureWidgetsSettings.SyncPresentation();

            SyncQuickCaptureSettingsFacade();
            SyncQuickCapturePresentationFacade();
            SyncQuickCaptureRecentLimitFacade();
            SyncQuickCaptureTextSizeFacade();
            SelectedAttachmentStorageMode = SettingsService.NormalizeAttachmentStorageMode(settings.AttachmentStorageMode);
            ApplyPerformanceSettingsSnapshot(settings);
            SyncQuickCaptureTabsFacade();

            _todoSettings.Refresh();
            SyncTodoTabFacade();
            SyncTodoDisplayFacade();
            SyncTodoTextSizeFacade();
            TodoUseWideDetailPane = _todoSettings.LayoutMode != SettingsService.TodoLayoutModeSinglePane;
            TodoAutoSelectFirstInWideLayout = _todoSettings.AutoSelectFirstInWideLayout;

            // Appearance presentation (material, density, window chrome, animation,
            // foreground, tray icon style) lives on the appearance editor now;
            // the shell only re-projects the selections whose state machines
            // stay here (theme, accent mode/effective color, group-nav).
            _appearanceSettings.SyncPresentation();
            PushAppearanceThemeSelection();
            UseSystemAccentColor = !string.Equals(
                settings.AccentColorMode,
                ThemeService.AccentModeCustom,
                StringComparison.OrdinalIgnoreCase);
            PushAppearanceAccentPresentation();

            // The group-navigation defaults and the capsule family's whole
            // presentation live on their section editors now: re-project them
            // from the coordinator snapshots instead of assigning shell
            // facade properties.
            _groupNavigationSettings.SyncPresentation();
            _capsuleSettings.SyncPresentation();


            // Music presentation lives on the section editor now: refresh the
            // editor projection instead of assigning shell facade properties.
            _musicSettings.SyncPresentation();

            // Interaction presentation (layer mode, snap enable/spacing,
            // open method, show-desktop behavior) lives on the section
            // editor now; the pushed hover summary follows the flyout state
            // re-projection above.
            _interactionSettings.SyncPresentation();
            _interactionSettings.UpdateHoverButtonActionsSummary(BuildHoverButtonActionsSummary());

            // File-display and managed-storage presentation live on their
            // section editors now: refresh the editor projections instead of
            // assigning shell facade properties. The root-path working state
            // below still feeds the shell's picker / migration /
            // quick-access chains.
            _fileDisplaySettings.SyncPresentation();
            _managedStorageSettings.SyncPresentation();

            WeatherAutoLocation = settings.WeatherAutoLocation;
            WeatherCityName = settings.WeatherCityName;
            WeatherCitySearchText = settings.WeatherCityName;
            SelectedWeatherTemperatureUnit = settings.WeatherTemperatureUnit == SettingsService.WeatherTemperatureUnitFahrenheit
                ? SettingsService.WeatherTemperatureUnitFahrenheit
                : SettingsService.WeatherTemperatureUnitCelsius;
            SelectedWeatherWindSpeedUnit = settings.WeatherWindSpeedUnit is SettingsService.WeatherWindSpeedUnitMs or SettingsService.WeatherWindSpeedUnitMph
                ? settings.WeatherWindSpeedUnit
                : SettingsService.WeatherWindSpeedUnitKmh;
            SelectedWeatherDefaultView = settings.WeatherDefaultView == SettingsService.WeatherDefaultViewWeek
                ? SettingsService.WeatherDefaultViewWeek
                : SettingsService.WeatherDefaultViewToday;
            SelectedWeatherSkin = settings.WeatherSkin == SettingsService.WeatherSkinRich
                ? SettingsService.WeatherSkinRich
                : SettingsService.WeatherSkinStandard;
            SelectedWeatherDataSource = settings.WeatherDataSource == SettingsService.WeatherDataSourceOpenMeteo
                ? SettingsService.WeatherDataSourceOpenMeteo
                : SettingsService.WeatherDataSourceMsn;
            WeatherShowForecast = settings.WeatherShowForecast;
            WeatherShowSunrise = settings.WeatherShowSunrise;
            WeatherShowUvIndex = settings.WeatherShowUvIndex;
            WeatherShowPrecipitation = settings.WeatherShowPrecipitation;
            WeatherShowHumidity = settings.WeatherShowHumidity;
            WeatherShowWind = settings.WeatherShowWind;
            WeatherShowPressure = settings.WeatherShowPressure;
            SelectedWeatherRefreshInterval = Math.Clamp(
                settings.WeatherRefreshIntervalMinutes,
                SettingsService.WeatherRefreshMinMinutes,
                SettingsService.WeatherRefreshMaxMinutes);

            ManagedStorageRootPath = SettingsService.NormalizeManagedStorageRootPath(settings.DefaultManagedStorageRootPath);
            _backupSettings.RefreshState();
        }
        finally
        {
            _isApplyingSettingsSnapshot = false;
            _isRestoringDefaults = wasRestoringDefaults;
        }

        RefreshSelectionProperties(refreshLocalizedOptions: false);
        RefreshGlobalHotkeyState();
        OnPropertyChanged(nameof(WeatherCityNameVisibility));
        OnPropertyChanged(nameof(QuickCaptureStatusText));
        OnPropertyChanged(nameof(QuickCaptureDependencyStatusText));
        OnPropertyChanged(nameof(FeatureWidgetEntries));
        NotifyCapsuleOverridePropertiesChanged();
        RefreshQuickCaptureClipboardDiagnostics();
        _ = RefreshQuickAccessStateAsync();
    }

    private void RefreshLocalizedProperties()
    {
        RefreshSelectionProperties(refreshLocalizedOptions: true);
        OnPropertyChanged(nameof(DistributionChannelText));
        OnPropertyChanged(nameof(OfficialWebsiteDisplayText));
        OnPropertyChanged(nameof(OpenSourceRepositoryDisplayText));
        OnPropertyChanged(nameof(UpdateDownloadActionText));
        OnPropertyChanged(nameof(StoreSupportCardVisibility));
        if (!IsCheckingForUpdates && !IsDownloadingUpdate)
        {
            if (_appUpdateService.LastCheckResult is not null)
            {
                ApplyCachedUpdateResult();
            }
            else
            {
                UpdateStatusText = _localizationService.T("Settings.Update.Status.Ready");
                UpdateDetailText = GetReadyUpdateDetailText();
            }
        }
        OnPropertyChanged(nameof(AutoStartStatusText));
        OnPropertyChanged(nameof(AvailableAutoStartModeOptions));
        // The global-hotkey card text lives on the interaction editor now;
        // the shell refreshes it through the editor push instead of shell
        // property notifications.
        NotifyDragDropPermissionPropertiesChanged();
        OnPropertyChanged(nameof(QuickCaptureStatusText));
        OnPropertyChanged(nameof(QuickCaptureDependencyStatusText));
        OnPropertyChanged(nameof(QuickCaptureRecentLimitText));
        OnPropertyChanged(nameof(FeatureWidgetEntries));
        NotifyCapsuleOverridePropertiesChanged();
        // The group-navigation editor rebuilds its option tables itself; the
        // existing-groups projection rebuild follows.
        RefreshWidgetGroupSettings();
        OnPropertyChanged(nameof(WeatherCitySearchPlaceholder));
        OnPropertyChanged(nameof(WeatherCityNoResultsText));
        RefreshWeatherCityPopularCities();
        RefreshQuickCaptureClipboardDiagnostics();
    }

    private void RefreshSelectionProperties(bool refreshLocalizedOptions)
    {
        // Replacing localized option arrays during an ordinary settings sync makes
        // WinUI reset every bound ComboBox.SelectedIndex to -1.
        if (refreshLocalizedOptions)
        {
            // The file-stack option tables, rule priorities, preview texts
            // and the overview summary re-project in the new language
            // through the section editor (batch 45).
            _fileStackSettings.RefreshLocalization();
            _featureWidgetsSettings.RefreshLocalization();
            _cachedLanguageDisplayNames = null;
            _cachedQuickCaptureDefaultViewDisplayNames = null;
            _cachedQuickCaptureTabStyleDisplayNames = null;
            _cachedTodoNewTaskPositionDisplayNames = null;
            _cachedAttachmentStorageModeDisplayNames = null;
            _cachedTodoDefaultFilterDisplayNames = null;
            _cachedTodoLayoutModeDisplayNames = null;
            _cachedTodoTabStyleDisplayNames = null;
            _cachedTodoReminderOffsetDisplayNames = null;
            _cachedWeatherTempUnitDisplayNames = null;
            _cachedWeatherWindUnitDisplayNames = null;
            _cachedWeatherDefaultViewDisplayNames = null;
            _cachedWeatherSkinDisplayNames = null;
            _cachedWeatherRefreshIntervalDisplayNames = null;
            _cachedAutomaticBackupIntervalDisplayNames = null;
            _cachedAutomaticBackupRetentionDisplayNames = null;
            OnPropertyChanged(nameof(AvailableLanguageDisplayNames));
            OnPropertyChanged(nameof(AvailableQuickCaptureDefaultViewDisplayNames));
            OnPropertyChanged(nameof(AvailableQuickCaptureTabStyleDisplayNames));
            OnPropertyChanged(nameof(AvailableTodoNewTaskPositionDisplayNames));
            OnPropertyChanged(nameof(AvailableAttachmentStorageModeDisplayNames));
            OnPropertyChanged(nameof(AvailableAutomaticBackupIntervalDisplayNames));
            OnPropertyChanged(nameof(AvailableAutomaticBackupRetentionDisplayNames));
            OnPropertyChanged(nameof(AvailableTodoDefaultFilterDisplayNames));
            OnPropertyChanged(nameof(AvailableTodoTabStyleDisplayNames));
            OnPropertyChanged(nameof(AvailableTodoReminderOffsetDisplayNames));
            OnPropertyChanged(nameof(AvailableWeatherTemperatureUnitDisplayNames));
            OnPropertyChanged(nameof(AvailableWeatherWindSpeedUnitDisplayNames));
            OnPropertyChanged(nameof(AvailableWeatherDefaultViewDisplayNames));
            OnPropertyChanged(nameof(AvailableWeatherSkinDisplayNames));
            OnPropertyChanged(nameof(AvailableWeatherRefreshIntervalDisplayNames));
            RefreshContentEditorLocalizedProperties();
            NotifySelectionOptionsChanged();
        }

        RefreshPerformanceSelectionProperties(refreshLocalizedOptions);

        OnPropertyChanged(nameof(SelectedLanguageText));
        NotifyHoverButtonActionPropertiesChanged();
        _interactionSettings.UpdateHoverButtonActionsSummary(BuildHoverButtonActionsSummary());
        OnPropertyChanged(nameof(SelectedQuickCaptureDefaultViewText));
        OnPropertyChanged(nameof(SelectedQuickCaptureTabStyleText));
        OnPropertyChanged(nameof(SelectedTodoNewTaskPositionText));
        OnPropertyChanged(nameof(SelectedTodoDefaultFilterText));
        OnPropertyChanged(nameof(SelectedTodoTabStyleText));
        OnPropertyChanged(nameof(SelectedTodoReminderOffsetMinutesText));
        OnPropertyChanged(nameof(QuickCaptureLayoutSummaryText));
        OnPropertyChanged(nameof(QuickCaptureTabStyleIndex));
        RefreshQuickCaptureTabsPresentation();
        RefreshQuickCaptureContentPresentation();
        OnPropertyChanged(nameof(TodoLayoutSummaryText));
        OnPropertyChanged(nameof(TodoWideOptionsVisibility));
        OnPropertyChanged(nameof(TodoTabStyleIndex));
        RefreshTodoTabsPresentation();
        RefreshTodoContentPresentation();
        OnPropertyChanged(nameof(TodoReminderSummaryText));
        OnPropertyChanged(nameof(TodoFooterDisplaySummaryText));
        OnPropertyChanged(nameof(WeatherDisplayOptionsSummaryText));
    }
}
