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
            SelectedTheme = settings.Theme is ThemeLight or ThemeDark ? settings.Theme : ThemeSystem;
            SelectedTrayIconStyle = settings.TrayIconStyle is TrayIconStyleColorful or TrayIconStyleBlack or TrayIconStyleWhite
                ? settings.TrayIconStyle
                : TrayIconStyleSystem;
            SelectedLanguage = LocalizationService.NormalizeLanguageSetting(settings.Language);
            UseSystemAccentColor = !string.Equals(
                settings.AccentColorMode,
                ThemeService.AccentModeCustom,
                StringComparison.OrdinalIgnoreCase);

            AutoCheckForUpdates = settings.AutoCheckForUpdates;
            FileItemSystemContextMenuEnabled = settings.FileItemSystemContextMenuEnabled;
            SelectedFileWidgetFolderOpenBehavior =
                FileWidgetFolderOpenBehaviorNames.NormalizeGlobal(
                    settings.FileWidgetFolderOpenBehavior);
            DefaultWidth = settings.DefaultWidgetWidth;
            DefaultHeight = settings.DefaultWidgetHeight;
            ShowHoverButtons = settings.ShowHoverButtons;
            ApplyHoverButtonActionSelection(settings.WidgetHoverButtonActions);

            WidgetOpacity = settings.WidgetOpacity;
            WidgetMaterialIntensity = settings.WidgetMaterialIntensity;
            ApplyWidgetForegroundSettingsSnapshot(settings);
            SelectedWidgetCornerPreference = WindowsCompatibilityService.ResolveEffectiveWidgetCornerPreference(
                settings.WidgetCornerPreference);
            SelectedWidgetMaterialType = WindowsCompatibilityService.ResolveWidgetMaterialType(
                settings.WidgetMaterialType);
            SelectedWidgetBorderColorMode = settings.WidgetBorderColorMode is BorderColorNeutral or BorderColorAccent or BorderColorNone
                ? settings.WidgetBorderColorMode
                : BorderColorNeutral;
            SelectedWidgetBorderStyle = settings.WidgetBorderStyle is BorderThin or BorderMedium or BorderThick
                ? settings.WidgetBorderStyle
                : BorderThin;

            SelectedWidgetCompactWidthMode = SettingsService.NormalizeWidgetCompactWidthMode(
                settings.WidgetCompactWidthMode);
            SelectedWidgetCompactExpansionDirection =
                SettingsService.NormalizeWidgetCompactExpansionDirection(
                    settings.WidgetCompactExpansionDirection);
            SelectedWidgetCapsuleArrangementMode = SettingsService.NormalizeWidgetCapsuleArrangementMode(
                settings.WidgetCapsuleArrangementMode);
            WidgetCapsuleBarSpacing = SettingsService.NormalizeWidgetCapsuleBarSpacing(
                settings.WidgetCapsuleBarSpacing);
            SelectedWidgetCapsuleBarPlacement = SettingsService.NormalizeWidgetCapsuleBarPlacement(
                settings.WidgetCapsuleBarPlacement);
            SelectedWidgetCapsuleBarDirection = SettingsService.NormalizeWidgetCapsuleBarDirection(
                settings.WidgetCapsuleBarDirection);
            WidgetCompactHideSensitiveContent = settings.WidgetCompactHideSensitiveContent;
            SelectedWidgetCollapseBehavior = SettingsService.NormalizeWidgetCollapseBehavior(
                settings.WidgetCollapseBehavior);
            SelectedWidgetCompactContentMode = SettingsService.NormalizeWidgetCompactContentMode(settings.WidgetCompactContentMode);
            SelectedWidgetCompactAnimationEffect = SettingsService.NormalizeWidgetCompactAnimationEffect(settings.WidgetCompactAnimationEffect);
            WidgetCompactAnimationDurationMs = SettingsService.NormalizeWidgetCompactAnimationDurationMs(settings.WidgetCompactAnimationDurationMs);
            WidgetCompactExpandDelayMs = SettingsService.NormalizeWidgetCompactExpandDelayMs(settings.WidgetCompactExpandDelayMs);
            WidgetCompactCollapseDelayMs = SettingsService.NormalizeWidgetCompactCollapseDelayMs(settings.WidgetCompactCollapseDelayMs);
            SelectedWidgetCompactHoverResponse = SettingsService.ResolveWidgetCompactHoverResponse(
                settings.WidgetCompactExpandDelayMs,
                settings.WidgetCompactCollapseDelayMs);
            SelectedWidgetCompactMediaCornerMode = SettingsService.NormalizeWidgetCompactMediaCornerMode(settings.WidgetCompactMediaCornerMode);

            SelectedWidgetAnimationEffect = NormalizeWidgetAnimationEffect(settings.WidgetAnimationEffect);
            SelectedWidgetAnimationSpeed = NormalizeWidgetAnimationSpeed(settings.WidgetAnimationSpeed);
            SelectedWidgetAnimationSlideDirection = NormalizeWidgetAnimationSlideDirection(settings.WidgetAnimationSlideDirection);
            SelectedWidgetAnimationEasingIntensity = NormalizeWidgetAnimationEasingIntensity(settings.WidgetAnimationEasingIntensity);
            SelectedAnimationPreset = ResolveAnimationPreset();
            SelectedDisplayWidgetChromeMode = NormalizeWidgetChromeModeSetting(
                settings.DisplayWidgetChromeMode,
                WidgetChromeMode.Overlay);
            SelectedInteractiveWidgetChromeMode = NormalizeWidgetChromeModeSetting(
                settings.InteractiveWidgetChromeMode,
                WidgetChromeMode.Standard);
            SelectedWidgetTitleIconMode = NormalizeWidgetTitleIconModeSetting(settings.WidgetTitleIconMode);

            IconSize = settings.IconSize;
            TextSize = settings.TextSize;
            LayoutDensityScale = settings.LayoutDensityScale;
            HorizontalSpacingScale = settings.HorizontalSpacingScale;
            VerticalSpacingScale = settings.VerticalSpacingScale;
            FileNameWidthScale = settings.FileNameWidthScale;
            FileNameLineCount = SettingsService.NormalizeFileNameLineCount(settings.FileNameLineCount);
            SelectedLayoutDensity = SettingsService.ResolveLayoutDensityPreset(settings);
            IdleWorkingSetTrimEnabled = settings.IdleWorkingSetTrimEnabled;
            ImmediateHiddenWorkingSetTrimEnabled = settings.ImmediateHiddenWorkingSetTrimEnabled;
            QuiescenceWorkingSetTrimEnabled = settings.Performance.QuiescenceWorkingSetTrimEnabled;

            ApplyContentEditorSettingsSnapshot(settings);
            ApplyFileStackSettingsSnapshot(settings);

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

        RefreshNumberInputs();
        RefreshSelectionProperties(refreshLocalizedOptions: false);
        RefreshGlobalHotkeyState();
        OnPropertyChanged(nameof(CanEditCustomAccent));
        OnPropertyChanged(nameof(AccentColorDescription));
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
        OnPropertyChanged(nameof(AccentColorDescription));
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
        OnPropertyChanged(nameof(AvailableWidgetGroupNavigationStyleOptions));
        OnPropertyChanged(nameof(AvailableWidgetGroupTitleDisplayModeOptions));
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
            RefreshFileStackSelectionProperties();
            _cachedThemeDisplayNames = null;
            _cachedTrayIconStyleDisplayNames = null;
            _cachedLanguageDisplayNames = null;
            _cachedWidgetCornerPreferenceDisplayNames = null;
            _cachedWidgetMaterialTypeDisplayNames = null;
            _cachedWidgetBorderColorModeDisplayNames = null;
            _cachedWidgetBorderStyleDisplayNames = null;
            _cachedWidgetCollapseBehaviorDisplayNames = null;
            _cachedWidgetCompactContentModeDisplayNames = null;
            _cachedWidgetCompactWidthModeDisplayNames = null;
            _cachedWidgetCompactExpansionDirectionDisplayNames = null;
            _cachedWidgetCapsuleArrangementDisplayNames = null;
            _cachedWidgetCapsuleBarPlacementDisplayNames = null;
            _cachedWidgetCapsuleBarDirectionDisplayNames = null;
            _cachedWidgetCompactAnimationEffectDisplayNames = null;
            _cachedWidgetCompactHoverResponseDisplayNames = null;
            _cachedWidgetCompactMediaCornerDisplayNames = null;
            _cachedLayoutDensityDisplayNames = null;
            _cachedAnimationPresetDisplayNames = null;
            _cachedWidgetAnimationEffectDisplayNames = null;
            _cachedWidgetAnimationSpeedDisplayNames = null;
            _cachedWidgetAnimationSlideDirectionDisplayNames = null;
            _cachedWidgetAnimationEasingIntensityDisplayNames = null;
            _cachedDisplayWidgetChromeModeDisplayNames = null;
            _cachedInteractiveWidgetChromeModeDisplayNames = null;
            _cachedWidgetTitleIconModeDisplayNames = null;
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
            OnPropertyChanged(nameof(AvailableThemeDisplayNames));
            OnPropertyChanged(nameof(AvailableTrayIconStyleDisplayNames));
            OnPropertyChanged(nameof(AvailableLanguageDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetCornerPreferenceDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetMaterialTypeDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetBorderColorModeDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetBorderStyleDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetCollapseBehaviorDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetCompactWidthModeDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetCompactExpansionDirectionDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetCompactContentModeDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetCapsuleArrangementDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetCapsuleBarPlacementDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetCapsuleBarDirectionDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetCompactAnimationEffectDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetCompactHoverResponseDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetCompactMediaCornerDisplayNames));
            OnPropertyChanged(nameof(AvailableLayoutDensityDisplayNames));
            OnPropertyChanged(nameof(AvailableAnimationPresetDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetAnimationEffectDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetAnimationSpeedDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetAnimationSlideDirectionDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetAnimationEasingIntensityDisplayNames));
            OnPropertyChanged(nameof(AvailableDisplayWidgetChromeModeDisplayNames));
            OnPropertyChanged(nameof(AvailableInteractiveWidgetChromeModeDisplayNames));
            OnPropertyChanged(nameof(AvailableWidgetTitleIconModeDisplayNames));
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

        RefreshWidgetForegroundSelectionProperties(refreshLocalizedOptions);
        RefreshPerformanceSelectionProperties(refreshLocalizedOptions);

        OnPropertyChanged(nameof(IsOpacitySliderEnabled));
        OnPropertyChanged(nameof(WidgetOpacityVisibility));
        OnPropertyChanged(nameof(MaterialIntensityVisibility));
        OnPropertyChanged(nameof(WidgetTransparency));
        OnPropertyChanged(nameof(IsWidgetBorderStyleEnabled));
        OnPropertyChanged(nameof(SelectedThemeText));
        OnPropertyChanged(nameof(SelectedTrayIconStyleText));
        OnPropertyChanged(nameof(SelectedLanguageText));
        OnPropertyChanged(nameof(SelectedWidgetCornerPreferenceText));
        OnPropertyChanged(nameof(SelectedWidgetMaterialTypeText));
        OnPropertyChanged(nameof(Windows10VisualCompatibilityTitle));
        OnPropertyChanged(nameof(Windows10VisualCompatibilityMessage));
        OnPropertyChanged(nameof(SelectedWidgetBorderColorModeText));
        OnPropertyChanged(nameof(SelectedWidgetBorderStyleText));
        OnPropertyChanged(nameof(SelectedWidgetCollapseBehaviorText));
        OnPropertyChanged(nameof(SelectedWidgetCompactWidthModeText));
        OnPropertyChanged(nameof(SelectedWidgetCompactExpansionDirectionText));
        OnPropertyChanged(nameof(IsSmartWidgetCollapseBehavior));
        OnPropertyChanged(nameof(IsSmartWidgetCollapseBehaviorSelected));
        OnPropertyChanged(nameof(CapsuleHoverResponseEntryVisibility));
        OnPropertyChanged(nameof(CanOpenWidgetCompactHoverResponseDetails));
        OnPropertyChanged(nameof(CanOpenWidgetCompactAnimationDetails));
        OnPropertyChanged(nameof(SelectedWidgetCapsuleArrangementText));
        OnPropertyChanged(nameof(IsWidgetCapsuleBarSelected));
        OnPropertyChanged(nameof(IsWidgetCapsuleBarEnabled));
        OnPropertyChanged(nameof(IsWidgetCapsuleBarSpacingEnabled));
        OnPropertyChanged(nameof(CapsuleArrangementEntryVisibility));
        OnPropertyChanged(nameof(WidgetCapsuleBarSpacingText));
        OnPropertyChanged(nameof(SelectedWidgetCapsuleBarPlacementText));
        OnPropertyChanged(nameof(SelectedWidgetCapsuleBarDirectionText));
        OnPropertyChanged(nameof(CapsuleArrangementDetailsSummaryText));
        OnPropertyChanged(nameof(SelectedWidgetCompactContentModeText));
        OnPropertyChanged(nameof(SelectedWidgetCompactAnimationEffectText));
        OnPropertyChanged(nameof(IsWidgetCompactAnimationCustom));
        OnPropertyChanged(nameof(WidgetCompactAnimationCustomVisibility));
        OnPropertyChanged(nameof(SelectedWidgetCompactHoverResponseText));
        OnPropertyChanged(nameof(IsWidgetCompactHoverResponseCustom));
        OnPropertyChanged(nameof(WidgetCompactHoverResponseCustomVisibility));
        OnPropertyChanged(nameof(SelectedWidgetCompactMediaCornerText));
        OnPropertyChanged(nameof(SelectedLayoutDensityText));
        OnPropertyChanged(nameof(SelectedAnimationPresetText));
        OnPropertyChanged(nameof(SelectedWidgetAnimationEffectText));
        OnPropertyChanged(nameof(IsDirectionEnabled));
        OnPropertyChanged(nameof(IsEasingEnabled));
        OnPropertyChanged(nameof(IsSpeedEnabled));
        OnPropertyChanged(nameof(SelectedWidgetAnimationSpeedText));
        OnPropertyChanged(nameof(SelectedWidgetAnimationSlideDirectionText));
        OnPropertyChanged(nameof(SelectedWidgetAnimationEasingIntensityText));
        OnPropertyChanged(nameof(SelectedDisplayWidgetChromeModeText));
        OnPropertyChanged(nameof(SelectedInteractiveWidgetChromeModeText));
        OnPropertyChanged(nameof(SelectedWidgetTitleIconModeText));
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
