using System.Globalization;
using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskBox.Contracts;
using DeskBox.Helpers;
using DeskBox.Models;
using DeskBox.Services;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace DeskBox.ViewModels;

/// <summary>
/// ViewModel for the settings window.
/// </summary>
public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private const string ThemeSystem = "System";
    private const string ThemeLight = "Light";
    private const string ThemeDark = "Dark";
    private const string TrayIconStyleSystem = "System";
    private const string TrayIconStyleColorful = "Colorful";
    private const string TrayIconStyleBlack = "Black";
    private const string TrayIconStyleWhite = "White";
    private const string CornerSquare = SettingsService.WidgetCornerPreferenceSquare;
    private const string CornerSmall = SettingsService.WidgetCornerPreferenceSmall;
    private const string CornerRound = SettingsService.WidgetCornerPreferenceRound;
    private const string MaterialMica = SettingsService.WidgetMaterialTypeMica;
    private const string MaterialMicaAlt = SettingsService.WidgetMaterialTypeMicaAlt;
    private const string MaterialAcrylic = SettingsService.WidgetMaterialTypeAcrylic;
    private const string MaterialAcrylicBase = SettingsService.WidgetMaterialTypeAcrylicBase;
    private const string MaterialSolid = SettingsService.WidgetMaterialTypeSolid;
    private const string BorderColorNeutral = SettingsService.WidgetBorderColorModeNeutral;
    private const string BorderColorAccent = SettingsService.WidgetBorderColorModeAccent;
    private const string BorderColorNone = SettingsService.WidgetBorderColorModeNone;
    private const string BorderNone = SettingsService.WidgetBorderStyleNone;
    private const string BorderThin = SettingsService.WidgetBorderStyleThin;
    private const string BorderMedium = SettingsService.WidgetBorderStyleMedium;
    private const string BorderThick = SettingsService.WidgetBorderStyleThick;
    private const string AnimationPresetGentle = "Gentle";
    private const string AnimationPresetStandard = "Standard";
    private const string AnimationPresetEmphasized = "Emphasized";
    private const string AnimationPresetCustom = "Custom";
    private const string WeatherLocationModeAuto = "Auto";
    private const string WeatherLocationModeManual = "Manual";
    private const string RepositoryUrl = "https://github.com/Tianyu199509/DeskBox";
    private const string OfficialWebsiteUrl = "https://deskbox.fun";
    private const string MicrosoftStoreProductId = "9PBZSNB4D69H";
    private const string MicrosoftStoreCampaignId = "deskbox_about_support";
    private const string MicrosoftStoreUrl =
        "https://apps.microsoft.com/detail/" + MicrosoftStoreProductId + "?cid=" + MicrosoftStoreCampaignId;
    private const string MicrosoftStoreAppUrl =
        "ms-windows-store://pdp/?ProductId=" + MicrosoftStoreProductId + "&cid=" + MicrosoftStoreCampaignId;

    private readonly SettingsService _settingsService;
    private readonly ThemeService _themeService;
    private readonly DeskBox.Features.Appearance.AppearanceSettingsViewModel _appearanceSettings;
    private readonly DeskBox.Features.Capsule.CapsuleSettingsViewModel _capsuleSettings;
    private readonly DeskBox.Features.Interaction.InteractionSettingsViewModel _interactionSettings;
    private readonly DeskBox.Features.FileDisplay.FileDisplaySettingsViewModel _fileDisplaySettings;
    private readonly DeskBox.Features.FileStack.FileStackSettingsViewModel _fileStackSettings;
    private readonly DeskBox.Features.GroupNavigation.GroupNavigationSettingsViewModel _groupNavigationSettings;
    private readonly DeskBox.Features.FeatureWidgets.FeatureWidgetsSettingsViewModel _featureWidgetsSettings;
    private readonly DeskBox.Features.Music.MusicSettingsViewModel _musicSettings;
    private readonly DeskBox.Features.ManagedStorage.ManagedStorageSettingsViewModel _managedStorageSettings;
    private readonly DeskBox.Features.Maintenance.MaintenanceSettingsViewModel _maintenanceSettings;
    private readonly LocalizationService _localizationService;
    private readonly WidgetContentFactory _widgetContentFactory;
    private readonly IAppUpdateService _appUpdateService;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private bool _isDisposed;
    private CancellationTokenSource? _updateOperationCts;
    private AppUpdateManifest? _availableUpdateManifest;
    private AppUpdateManifest? _latestUpdateManifest;
    private string? _downloadedUpdateInstallerPath;
    private bool _showManualUpdateFallback;
    private bool _lastUpdateDownloadFailed;
    private long _updateBytesReceived;
    private long? _updateTotalBytes;
    private Color _currentAccentColor;
    private string _selectedLanguage = SettingsService.LanguageSystem;
    private string _selectedAttachmentStorageMode = SettingsService.AttachmentStorageModeLink;
    private string _selectedWeatherTemperatureUnit = SettingsService.WeatherTemperatureUnitCelsius;
    private string _selectedWeatherWindSpeedUnit = SettingsService.WeatherWindSpeedUnitKmh;
    private string _selectedWeatherDefaultView = SettingsService.WeatherDefaultViewToday;
    private string _selectedWeatherSkin = SettingsService.WeatherSkinRich;
    private string _selectedWeatherDataSource = SettingsService.WeatherDataSourceMsn;
    private int _selectedWeatherRefreshInterval = 60;
    private bool _useSystemAccentColor;
    private string _managedStorageRootPath = SettingsService.GetDefaultManagedStorageRootPath();
    private QuickAccessPinState _quickAccessPinState = QuickAccessPinState.Unknown;
    private bool _isQuickAccessBusy;
    private string _quickCaptureImageCacheText = string.Empty;
    private string _quickCaptureClipboardDiagnosticsText = string.Empty;
    private StartupRegistrationState _autoStartState =
        StartupRegistrationState.NotRegistered;
    private DragDropPermissionDiagnostic? _dragDropPermissionDiagnostic;
    private string _dragDropPermissionRepairStatusText = string.Empty;
    private bool _isDragDropPermissionRepairing;
    private bool _canClearQuickCaptureImageCache;
    private bool _isRestoringDefaults;
    private bool _isApplyingSettingsSnapshot;
    private bool _isUpdatingHoverButtonActionSelection;

    private string[]? _cachedLanguageDisplayNames;
    private string[]? _cachedQuickCaptureDefaultViewDisplayNames;
    private string[]? _cachedQuickCaptureTabStyleDisplayNames;
    private string[]? _cachedTodoNewTaskPositionDisplayNames;
    private string[]? _cachedAttachmentStorageModeDisplayNames;
    private string[]? _cachedTodoDefaultFilterDisplayNames;
    private string[]? _cachedTodoLayoutModeDisplayNames;
    private string[]? _cachedTodoTabStyleDisplayNames;
    private string[]? _cachedTodoReminderOffsetDisplayNames;
private string[]? _cachedWeatherTempUnitDisplayNames;
private string[]? _cachedWeatherWindUnitDisplayNames;
private string[]? _cachedWeatherDefaultViewDisplayNames;
private string[]? _cachedWeatherSkinDisplayNames;
private string[]? _cachedWeatherDataSourceDisplayNames;
private string[]? _cachedWeatherRefreshIntervalDisplayNames;

    [ObservableProperty] public partial bool AutoStart { get; set; }
    private bool _autoStartUsedFallback;
    private bool _autoStartOperationFailed;
    [ObservableProperty] public partial string SelectedAutoStartMode { get; set; } = nameof(StartupMode.Standard);
    public object[] AvailableAutoStartModeOptions =>
    [
        new SettingsOption(nameof(StartupMode.Standard), _localizationService.T("Settings.AutoStart.Mode.Standard")),
        new SettingsOption(nameof(StartupMode.ScheduledTask), _localizationService.T("Settings.AutoStart.Mode.ScheduledTask"))
    ];
    // Keep the choice available while off, including when Standard cannot be
    // registered (for example an executable command longer than Run's limit).
    // SetMode only records a preference while startup is disabled.
    public Visibility AutoStartModeVisibility => StartupService.Current is DirectStartupService
        ? Visibility.Visible : Visibility.Collapsed;
    public string AutoStartStatusText => _autoStartState switch
    {
        StartupRegistrationState.DisabledByUser =>
            _localizationService.T("Settings.AutoStart.WindowsDisabled"),
        StartupRegistrationState.DisabledByTaskScheduler =>
            _localizationService.T("Settings.AutoStart.TaskDisabled"),
        StartupRegistrationState.Pending =>
            _localizationService.T("Settings.AutoStart.Pending"),
        StartupRegistrationState.Enabled when _autoStartUsedFallback =>
            _localizationService.T("Settings.AutoStart.Fallback"),
        _ when _autoStartOperationFailed =>
            _localizationService.T("Settings.AutoStart.ChangeFailed"),
        StartupRegistrationState.PathMismatch or
        StartupRegistrationState.BlockedOrFailed =>
            _localizationService.T("Settings.AutoStart.Failed"),
        _ => string.Empty
    };
    public Visibility AutoStartStatusVisibility => string.IsNullOrEmpty(AutoStartStatusText)
        ? Visibility.Collapsed : Visibility.Visible;
    public Visibility AutoStartSystemSettingsVisibility =>
        _autoStartState == StartupRegistrationState.DisabledByUser
            ? Visibility.Visible
            : Visibility.Collapsed;
    [ObservableProperty] public partial bool AutoCheckForUpdates { get; set; } = true;
    [ObservableProperty] public partial bool ShowHoverButtons { get; set; } = true;
    [ObservableProperty] public partial bool ShowHoverActionLockPosition { get; set; }
    [ObservableProperty] public partial bool ShowHoverActionLockSize { get; set; }
    [ObservableProperty] public partial bool ShowHoverActionAdd { get; set; } = true;
    [ObservableProperty] public partial bool ShowHoverActionMore { get; set; } = true;
    [ObservableProperty] public partial bool ShowHoverActionDelete { get; set; } = true;
    [ObservableProperty] public partial bool IdleWorkingSetTrimEnabled { get; set; } = true;
    [ObservableProperty] public partial bool ImmediateHiddenWorkingSetTrimEnabled { get; set; }
    [ObservableProperty] public partial bool QuiescenceWorkingSetTrimEnabled { get; set; } = true;
    [ObservableProperty] public partial bool QuickCaptureEnabled { get; set; }
    [ObservableProperty] public partial bool QuickCaptureShowTabBar { get; set; } = true;
    [ObservableProperty] public partial bool QuickCaptureShowRecordsTab { get; set; } = true;
    [ObservableProperty] public partial bool QuickCaptureShowPinnedTab { get; set; } = true;
    [ObservableProperty] public partial bool QuickCaptureShowRecentTab { get; set; } = true;
    [ObservableProperty] public partial bool TodoShowTabBar { get; set; } = true;
    [ObservableProperty] public partial bool TodoShowAllTab { get; set; } = true;
    [ObservableProperty] public partial bool TodoShowActiveTab { get; set; }
    [ObservableProperty] public partial bool TodoShowTodayTab { get; set; } = true;
    [ObservableProperty] public partial bool TodoShowThisWeekTab { get; set; }
    [ObservableProperty] public partial bool TodoShowThisMonthTab { get; set; }
    [ObservableProperty] public partial bool TodoShowImportantTab { get; set; } = true;
    [ObservableProperty] public partial bool TodoShowCompletedTab { get; set; } = true;
    [ObservableProperty] public partial bool TodoShowCompletedTasks { get; set; } = true;
    [ObservableProperty] public partial bool TodoShowFooterStats { get; set; }
    [ObservableProperty] public partial bool TodoShowClearCompletedButton { get; set; } = true;
    [ObservableProperty] public partial bool TodoUseWideDetailPane { get; set; } = true;
    [ObservableProperty] public partial bool TodoAutoSelectFirstInWideLayout { get; set; } = true;

    [ObservableProperty] public partial bool WeatherAutoLocation { get; set; } = true;
    [ObservableProperty] public partial string WeatherCityName { get; set; } = string.Empty;
    [ObservableProperty] public partial bool WeatherShowForecast { get; set; } = true;
    [ObservableProperty] public partial bool WeatherShowSunrise { get; set; } = true;
    [ObservableProperty] public partial bool WeatherShowUvIndex { get; set; } = true;
    [ObservableProperty] public partial bool WeatherShowPrecipitation { get; set; } = true;
    [ObservableProperty] public partial bool WeatherShowHumidity { get; set; } = true;
    [ObservableProperty] public partial bool WeatherShowWind { get; set; } = true;
    [ObservableProperty] public partial bool WeatherShowPressure { get; set; }

    [ObservableProperty] public partial bool QuickCaptureClipboardEnabled { get; set; }
    [ObservableProperty] public partial bool QuickCaptureImageClipboardEnabled { get; set; }
    [ObservableProperty] public partial int QuickCaptureRecentLimit { get; set; } = QuickCaptureService.DefaultRecentLimit;
    [ObservableProperty] public partial bool QuickCaptureShowCreatedTime { get; set; } = true;
    [ObservableProperty] public partial double QuickCaptureListTextSize { get; set; } = SettingsService.DefaultTextSize;
    [ObservableProperty] public partial double QuickCaptureContentTextSize { get; set; } = SettingsService.DefaultTextSize;
    [ObservableProperty] public partial double TodoListTextSize { get; set; } = SettingsService.DefaultTextSize;
    [ObservableProperty] public partial double TodoContentTextSize { get; set; } = SettingsService.DefaultTextSize;
    [ObservableProperty] public partial bool IsCheckingForUpdates { get; set; }
    [ObservableProperty] public partial bool IsDownloadingUpdate { get; set; }
    [ObservableProperty] public partial string UpdateStatusText { get; set; } = string.Empty;
    [ObservableProperty] public partial string UpdateDetailText { get; set; } = string.Empty;
    [ObservableProperty] public partial double UpdateProgressValue { get; set; }

    public SettingsViewModel(
        SettingsService settingsService,
        ThemeService themeService,
        DeskBox.Features.Todo.TodoSettingsViewModel todoSettings,
        DeskBox.Features.Backup.BackupSettingsViewModel backupSettings,
        DeskBox.Contracts.IQuickCaptureSettings quickCaptureSettings,
        DeskBox.Contracts.ISearchFeatureSettings searchFeatureSettings,
        DeskBox.Features.Appearance.AppearanceSettingsViewModel appearanceSettings,
        DeskBox.Features.Capsule.CapsuleSettingsViewModel capsuleSettings,
        DeskBox.Features.Interaction.InteractionSettingsViewModel interactionSettings,
        DeskBox.Features.FileDisplay.FileDisplaySettingsViewModel fileDisplaySettings,
        DeskBox.Features.FileStack.FileStackSettingsViewModel fileStackSettings,
        DeskBox.Features.GroupNavigation.GroupNavigationSettingsViewModel groupNavigationSettings,
        DeskBox.Features.FeatureWidgets.FeatureWidgetsSettingsViewModel featureWidgetsSettings,
        DeskBox.Features.Music.MusicSettingsViewModel musicSettings,
        DeskBox.Features.ManagedStorage.ManagedStorageSettingsViewModel managedStorageSettings,
        DeskBox.Features.Maintenance.MaintenanceSettingsViewModel maintenanceSettings,
        LocalizationService? localizationService = null,
        IAppUpdateService? appUpdateService = null)
    {
        _settingsService = settingsService;
        _todoSettings = todoSettings;
        _todoSettings.PropertyChanged += OnTodoSettingsPropertyChanged;
        _backupSettings = backupSettings;
        _backupSettings.PropertyChanged += OnBackupSettingsPropertyChanged;
        _quickCaptureSettings = quickCaptureSettings;
        _searchFeatureSettings = searchFeatureSettings;
        _appearanceSettings = appearanceSettings;
        _capsuleSettings = capsuleSettings;
        _interactionSettings = interactionSettings;
        _fileDisplaySettings = fileDisplaySettings;
        _fileStackSettings = fileStackSettings;
        _groupNavigationSettings = groupNavigationSettings;
        _featureWidgetsSettings = featureWidgetsSettings;
        _musicSettings = musicSettings;
        _managedStorageSettings = managedStorageSettings;
        _maintenanceSettings = maintenanceSettings;
        _themeService = themeService;
        _localizationService = localizationService ?? new LocalizationService(settingsService);
        _widgetContentFactory = new WidgetContentFactory(_localizationService);
        _appUpdateService = appUpdateService ?? new AppUpdateService();
        _isRestoringDefaults = true;
        _quickCaptureImageCacheText = _localizationService.T("Settings.QuickCapture.ImageCacheLoading");
        _quickCaptureClipboardDiagnosticsText = _localizationService.T("Settings.QuickCapture.ClipboardDiagnosticsUnavailable");
        _dragDropPermissionRepairStatusText = string.Empty;
        UpdateStatusText = _localizationService.T("Settings.Update.Status.Ready");
        UpdateDetailText = GetReadyUpdateDetailText();

        var settings = settingsService.Settings;
        _selectedLanguage = LocalizationService.NormalizeLanguageSetting(settings.Language);

        _useSystemAccentColor = !string.Equals(settings.AccentColorMode, ThemeService.AccentModeCustom, StringComparison.OrdinalIgnoreCase);
        AutoStart = StartupService.IsEnabled();
        _autoStartState = AutoStart
            ? StartupRegistrationState.Enabled
            : StartupService.GetState();
        if (StartupService.Current is DirectStartupService directStartup)
            SelectedAutoStartMode = directStartup.Mode.ToString();
        AutoCheckForUpdates = settings.AutoCheckForUpdates;
        ShowHoverButtons = settings.ShowHoverButtons;
        ApplyHoverButtonActionSelection(settings.WidgetHoverButtonActions);
        // The file-stack section's presentation (including the custom-rule
        // collection and its aggregation) lives on the file-stack editor now
        // (batch 45); its constructor syncs itself from the coordinator
        // snapshot. The rule-preview entries are widget items at first, and
        // the shell pushes the freshly scanned disk entries whenever the
        // section is entered.
        _fileStackPreviewEntries = BuildFileStackPreviewEntries(includeMappedFolders: false);
        _fileStackSettings.UpdatePreviewEntries(_fileStackPreviewEntries);
        InitializeContentEditorSettings(settings);
        InitializePerformanceSettings(settings);
        // The capsule family's presentation lives on the capsule editor now
        // (batch 44); its constructor syncs itself from the coordinator
        // snapshots, and the widget/group override projection is pushed in
        // right after the editor fields are wired below.
        IdleWorkingSetTrimEnabled = settings.IdleWorkingSetTrimEnabled;
        ImmediateHiddenWorkingSetTrimEnabled = settings.ImmediateHiddenWorkingSetTrimEnabled;
        QuiescenceWorkingSetTrimEnabled = settings.Performance.QuiescenceWorkingSetTrimEnabled;
        SyncQuickCaptureSettingsFacade();
        SyncQuickCapturePresentationFacade();
        SyncQuickCaptureRecentLimitFacade();
        SyncQuickCaptureTextSizeFacade();
        _selectedAttachmentStorageMode = SettingsService.NormalizeAttachmentStorageMode(settings.AttachmentStorageMode);
        SyncQuickCaptureTabsFacade();
        SyncTodoTabFacade();
        SyncTodoDisplayFacade();
        SyncTodoTextSizeFacade();
        TodoUseWideDetailPane = _todoSettings.LayoutMode != SettingsService.TodoLayoutModeSinglePane;
        TodoAutoSelectFirstInWideLayout = _todoSettings.AutoSelectFirstInWideLayout;
        WeatherAutoLocation = settings.WeatherAutoLocation;
        WeatherCityName = settings.WeatherCityName;
        _weatherCitySearchText = settings.WeatherCityName;
        _selectedWeatherTemperatureUnit = settings.WeatherTemperatureUnit == SettingsService.WeatherTemperatureUnitFahrenheit
            ? SettingsService.WeatherTemperatureUnitFahrenheit
            : SettingsService.WeatherTemperatureUnitCelsius;
        _selectedWeatherWindSpeedUnit = settings.WeatherWindSpeedUnit is SettingsService.WeatherWindSpeedUnitMs or SettingsService.WeatherWindSpeedUnitMph
            ? settings.WeatherWindSpeedUnit
            : SettingsService.WeatherWindSpeedUnitKmh;
        _selectedWeatherDefaultView = settings.WeatherDefaultView == SettingsService.WeatherDefaultViewWeek
            ? SettingsService.WeatherDefaultViewWeek
            : SettingsService.WeatherDefaultViewToday;
        _selectedWeatherSkin = settings.WeatherSkin == SettingsService.WeatherSkinRich
            ? SettingsService.WeatherSkinRich
            : SettingsService.WeatherSkinStandard;
        WeatherShowForecast = settings.WeatherShowForecast;
        WeatherShowSunrise = settings.WeatherShowSunrise;
        WeatherShowUvIndex = settings.WeatherShowUvIndex;
        WeatherShowPrecipitation = settings.WeatherShowPrecipitation;
        WeatherShowHumidity = settings.WeatherShowHumidity;
        WeatherShowWind = settings.WeatherShowWind;
        WeatherShowPressure = settings.WeatherShowPressure;
        _selectedWeatherRefreshInterval = Math.Clamp(
            settings.WeatherRefreshIntervalMinutes,
            SettingsService.WeatherRefreshMinMinutes,
            SettingsService.WeatherRefreshMaxMinutes);
        _isRestoringDefaults = false;
        _managedStorageRootPath = settings.DefaultManagedStorageRootPath;
        SyncBackupSettingsFacade();

        ApplyCachedUpdateResult();
        RefreshAccentPreview();
        RefreshDragDropPermissionDiagnostic();
// The managed-storage editor shows the quick-access card; project the initial
// unknown state now and let the async refresh push the live pin state.
PushQuickAccessPresentation();
_ = PopulateNearbyPopularCitiesAsync();
_ = RefreshQuickAccessStateAsync();
        _settingsService.SettingsChanged += OnSettingsChanged;
        _quickCaptureSettings.Changed += OnQuickCaptureSettingsChanged;
        _quickCaptureSettings.DiagnosticsChanged += OnQuickCaptureClipboardDiagnosticsChanged;
        _searchFeatureSettings.FeatureChanged += OnSearchFeatureChanged;
        _themeService.AppearanceChanged += OnAppearanceChanged;
        _localizationService.LanguageChanged += OnLanguageChanged;
        RefreshQuickCaptureClipboardDiagnostics();

        // Interaction-section host linkages: the editor owns the section's
        // binding surface and persisted writes, the shell still owns the
        // host-side effects that ran around the legacy facade writes. The
        // handlers live in the partials that already carried them.
        _interactionSettings.LayerModeUserChanged += OnInteractionLayerModeUserChanged;
        _interactionSettings.ShowDesktopBehaviorUserChanged += OnInteractionShowDesktopBehaviorUserChanged;
        _interactionSettings.SnapEnabledUserChanged += OnInteractionSnapEnabledUserChanged;
        _interactionSettings.SnapSpacingUserChanged += OnInteractionSnapSpacingUserChanged;
        _interactionSettings.HotkeyEnabledUserChanged += OnInteractionHotkeyEnabledUserChanged;
        _interactionSettings.FileItemContextMenuEnabledUserChanged += OnInteractionFileItemContextMenuUserChanged;
        _interactionSettings.UpdateHoverButtonActionsSummary(BuildHoverButtonActionsSummary());

        // Appearance-section host linkages: the editor owns the section
        // family's binding surface and persisted writes, the shell still owns
        // the live-preview orchestration and the theme/accent/group-nav state
        // machines. The handlers live in the appearance-options partial.
        _appearanceSettings.AppearanceValueCommitted += OnAppearanceValueCommitted;
        _appearanceSettings.TextSizeCommitted += OnAppearanceTextSizeCommitted;
        _appearanceSettings.LayoutDensityMarkedCustom += OnAppearanceLayoutDensityMarkedCustom;
        _appearanceSettings.AnimationPresetApplied += OnAppearanceAnimationPresetApplied;
        _appearanceSettings.ThemeUserChanged += OnAppearanceThemeUserChanged;
        _appearanceSettings.TrayIconStyleUserChanged += OnAppearanceTrayIconStyleUserChanged;
        _appearanceSettings.AccentColorSourceUserChanged += OnAppearanceAccentColorSourceUserChanged;
        _appearanceSettings.AccentColorUserChanged += OnAppearanceAccentColorUserChanged;
        PushAppearanceHostEnvironment();
        PushAppearanceThemeSelection();

        // Group-navigation and capsule host linkages (batch 44): the editors
        // own the section binding surfaces; the shell answers user edits with
        // the explicit widget-group presentation notification and rebuilds
        // the pushed existing-groups / override projections.
        _groupNavigationSettings.PresentationUserChanged += OnGroupNavigationPresentationUserChanged;
        NotifyExistingWidgetGroupPropertiesChanged();
        NotifyCapsuleOverridePropertiesChanged();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _settingsService.SaveAsync();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _todoSettings.PropertyChanged -= OnTodoSettingsPropertyChanged;
        _todoSettings.Dispose();
        _backupSettings.PropertyChanged -= OnBackupSettingsPropertyChanged;
        _backupSettings.Dispose();
        _lifetimeCts.Cancel();
        _updateOperationCts?.Cancel();
        _updateOperationCts?.Dispose();
        _quickCaptureSettings.Changed -= OnQuickCaptureSettingsChanged;
        _quickCaptureSettings.DiagnosticsChanged -= OnQuickCaptureClipboardDiagnosticsChanged;
        _searchFeatureSettings.FeatureChanged -= OnSearchFeatureChanged;

        _settingsService.SettingsChanged -= OnSettingsChanged;
        _themeService.AppearanceChanged -= OnAppearanceChanged;
        _localizationService.LanguageChanged -= OnLanguageChanged;
        _citySearchCts?.Cancel();
        _citySearchCts?.Dispose();
        _citySearchService?.Dispose();
        _lifetimeCts.Dispose();
    }

    private void OnAppearanceChanged()
    {
        RefreshAccentPreview();
    }

    private void RefreshAccentPreview()
    {
        _currentAccentColor = _themeService.GetEffectiveAccentColor();
        // The accent card lives on the appearance editor; push the effective
        // color and mode projection instead of mirroring shell properties.
        PushAppearanceAccentPresentation();
    }

    private static string FormatNumber(double value, int decimals)
    {
        string format = decimals <= 0 ? "0" : $"0.{new string('#', decimals)}";
        return value.ToString(format, CultureInfo.CurrentCulture);
    }

    public string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return string.Format(CultureInfo.CurrentCulture, 
                $"{Math.Max(0, bytes)} {_localizationService.T("Size.Unit.Bytes")}", 
                CultureInfo.CurrentCulture);
        }

        var units = new[] 
        {
            _localizationService.T("Size.Unit.KB"),
            _localizationService.T("Size.Unit.MB"),
            _localizationService.T("Size.Unit.GB")
        };
        double value = bytes;
        int unitIndex = -1;
        do
        {
            value /= 1024d;
            unitIndex++;
        }
        while (value >= 1024d && unitIndex < units.Length - 1);

        return string.Format(CultureInfo.CurrentCulture, 
            $"{value:0.#} {units[unitIndex]}", 
            CultureInfo.CurrentCulture);
    }

    private void ApplyQuickCaptureRecentLimitInput(string? value)
    {
        if (!TryParseNumberInput(value, out double parsedValue))
        {
            OnPropertyChanged(nameof(QuickCaptureRecentLimitInput));
            return;
        }

        int normalizedValue = QuickCaptureService.NormalizeRecentLimit((int)Math.Round(parsedValue, MidpointRounding.AwayFromZero));
        if (normalizedValue != QuickCaptureRecentLimit)
        {
            QuickCaptureRecentLimit = normalizedValue;
        }

        OnPropertyChanged(nameof(QuickCaptureRecentLimitInput));
    }

    private static bool TryParseNumberInput(string? value, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out result) ||
               double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private void SaveAppearanceChange()
    {
        if (DeferAppearancePersistence)
        {
            _settingsService.RequestAppearancePreview();
            return;
        }

        if (!SuppressAppearanceNotifications)
        {
            _settingsService.RequestAppearancePreview();
        }

        _settingsService.SaveDebounced(
            notifySubscribers: !SuppressAppearanceNotifications,
            changeKind: SettingsChangeKind.Appearance);
    }

}
