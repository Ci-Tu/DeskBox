using System.Diagnostics;
using CommunityToolkit.WinUI.Controls;
using DeskBox.Services;
using DeskBox.Views.SettingsSections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace DeskBox.Views;

public sealed partial class SettingsWindow
{
    private void RefreshVisibleSettingsPageData()
    {
        UpdateSearchSettingsActivity();
        UpdateBackupSettingsActivity();
        if (_currentSettingsSection is "AppearanceDetail" or "FileStorageSettings")
        {
            RefreshManagedStoragePathWarning();
            RefreshManagedStorageDesktopShortcutState();
            _ = ViewModel.RefreshQuickAccessStateAsync();
        }
        if (_currentSettingsSection is "Interaction" or "Advanced")
        {
            ViewModel.RefreshGlobalHotkeyState();
            RefreshGlobalHotkeyControls();
        }
        if (_currentSettingsSection == "QuickCaptureSettings")
        {
            _ = ViewModel.RefreshQuickCaptureImageCacheInfoAsync();
        }
        if (_currentSettingsSection == "SearchSettings" &&
            _settingsSectionElements.TryGetValue("SearchSettings", out FrameworkElement? search) &&
            search is SearchSettingsSection section)
        {
            section.RefreshFromSettings();
        }
    }

    private void UpdateSearchSettingsActivity()
    {
        if (_settingsSectionElements.TryGetValue("SearchSettings", out FrameworkElement? element) &&
            element is SearchSettingsSection section)
        {
            section.SetActive(!_isClosed && IsVisibleToUser && _currentSettingsSection == "SearchSettings");
        }
    }

    private void UpdateBackupSettingsActivity()
    {
        if (!_isClosed && IsVisibleToUser && _currentSettingsSection == "CloudBackupSettings")
            _backupSettingsViewModel.Activate();
        else
            _backupSettingsViewModel.Deactivate();
    }

    private FrameworkElement EnsureSettingsSectionCreated(string sectionTag)
    {
        if (_settingsSectionElements.TryGetValue(sectionTag, out FrameworkElement? existing))
        {
            return existing;
        }
        if (!ContentHost.Resources.TryGetValue(sectionTag + "SectionTemplate", out object resource) ||
            resource is not DataTemplate template)
        {
            throw new InvalidOperationException($"Settings section '{sectionTag}' is not registered.");
        }

        var stopwatch = Stopwatch.StartNew();
        FrameworkElement section = (FrameworkElement)template.LoadContent();
        _settingsSectionElements.Add(sectionTag, section);
        section.DataContext = ViewModel;
        // LoadContent is used outside an ItemsControl, so initialize the
        // template's generated compiled bindings with the actual view model.
        // The file-stack template keeps its compiled x:Bind surface typed to
        // the section editor (batch 45), so its bindings initialize with the
        // editor instead of the shell view model.
        object compiledBindingsRoot = sectionTag == "FileStackSettings"
            ? _fileStackSettingsViewModel
            : ViewModel;
        XamlBindingHelper.GetDataTemplateComponent(section)?.ProcessBindings(compiledBindingsRoot, 0, 0, out _);

        switch (section)
        {
            case SearchSettingsSection searchSettings:
                searchSettings.Configure(_searchSettingsViewModel, _localizationService, _hWnd);
                break;
            case FileWidgetSettingsSection fileSettings:
                fileSettings.FileStack = _fileStackSettingsViewModel;
                fileSettings.FeatureWidgets = _featureWidgetsSettingsViewModel;
                fileSettings.Interaction = _interactionSettingsViewModel;
                break;
            case CapsuleModeSettingsSection capsuleSettings:
                capsuleSettings.ViewModel = ViewModel;
                break;
            case GlanceWidgetSettingsSection glanceSettings:
                glanceSettings.SetOwnerWindow(_hWnd);
                break;
        }

        // Pilot pattern for retiring the shell binding facade: sections whose
        // editor owns the binding surface get the editor as their DataContext,
        // overriding the shell view model set above. {Binding} markup resolves
        // through the editor's generated custom-property provider under
        // Native AOT, so no per-property shell bridge is needed anymore.
        if (sectionTag == "MusicSettings")
        {
            section.DataContext = _musicSettingsViewModel;
        }

        if (sectionTag is "Interaction" or "InteractionWindowSettings")
        {
            section.DataContext = _interactionSettingsViewModel;
        }

        // The file-stack section binds through the file-stack editor
        // (batch 45): {Binding} markup resolves through its generated custom
        // property provider under Native AOT, and the template's compiled
        // x:Bind paths (open-mode combo and the rule list) are typed to the
        // editor as well.
        if (sectionTag == "FileStackSettings")
        {
            section.DataContext = _fileStackSettingsViewModel;
        }

        if (sectionTag == "FileDisplaySettings")
        {
            section.DataContext = _fileDisplaySettingsViewModel;
        }

        if (sectionTag == "FileStorageSettings")
        {
            section.DataContext = _managedStorageSettingsViewModel;
            RefreshManagedStoragePathWarning();
            RefreshManagedStorageDesktopShortcutState();
        }

        // The appearance family (main section plus the material, density,
        // window and animation subsections) binds through the appearance
        // editor; {Binding} markup resolves through its generated custom
        // property provider under Native AOT.
        if (sectionTag is "Appearance" or
            "AppearanceMaterialSettings" or
            "AppearanceDensitySettings" or
            "AppearanceWindowSettings" or
            "AppearanceAnimationSettings")
        {
            section.DataContext = _appearanceSettingsViewModel;
        }

        // The WidgetGroups section binds through the group-navigation
        // editor (batch 44); the four defaults are the editor's own persisted
        // surface and the existing-groups projection is pushed in by the
        // shell's group-editing state machine.
        if (sectionTag == "WidgetGroups")
        {
            section.DataContext = _groupNavigationSettingsViewModel;
        }

        // The capsule family (main capsule section plus the behavior,
        // arrangement, animation and overrides subsections) binds through the
        // capsule editor (batch 44); the override-list projection is pushed
        // in by the shell's override state machine.
        if (sectionTag is "CapsuleMode" or
            "CapsuleBehaviorSettings" or
            "CapsuleArrangementSettings" or
            "CapsuleAnimationSettings" or
            "CapsuleOverridesSettings")
        {
            section.DataContext = _capsuleSettingsViewModel;
        }

        if (sectionTag == "InteractionWindowSettings")
        {
            ViewModel.RefreshGlobalHotkeyState();
            RefreshGlobalHotkeyControls();
        }

        section.Loaded += DeferredSettingsSection_Loaded;
        ContentHost.Children.Add(section);
        App.Log(
            $"[SettingsPerf] Section created tag={sectionTag} " +
            $"createdSections={_settingsSectionElements.Count} elapsedMs={stopwatch.ElapsedMilliseconds}");
        return section;
    }

    private void DeferredSettingsSection_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isClosed || sender is not FrameworkElement section)
        {
            return;
        }
        section.Loaded -= DeferredSettingsSection_Loaded;
        ApplyToggleSwitchContentVisibility();
        CollectResponsiveRows(SettingsRoot);
        UpdateResponsiveLayout(GetWindowWidth());
    }

    private static FrameworkElement? FindSettingsSearchTarget(
        DependencyObject root,
        string headerKey,
        HashSet<DependencyObject> visited)
    {
        if (!visited.Add(root))
        {
            return null;
        }
        if (root is FrameworkElement element &&
            string.Equals(Localized.GetHeaderKey(element), headerKey, StringComparison.Ordinal))
        {
            return element;
        }

        // Collapsed expander items already exist in the realized section but
        // are not necessarily visual children yet. Expand only the matching
        // branch so search can reveal a setting without opening every group.
        if (root is SettingsExpander expander)
        {
            foreach (DependencyObject item in expander.Items.OfType<DependencyObject>())
            {
                if (FindSettingsSearchTarget(item, headerKey, visited) is { } target)
                {
                    expander.IsExpanded = true;
                    return target;
                }
            }
        }
        if (root is Expander { Content: DependencyObject expanderContent } nativeExpander &&
            FindSettingsSearchTarget(expanderContent, headerKey, visited) is { } expanderTarget)
        {
            nativeExpander.IsExpanded = true;
            return expanderTarget;
        }
        if (root is ContentControl { Content: DependencyObject content } &&
            FindSettingsSearchTarget(content, headerKey, visited) is { } contentTarget)
        {
            return contentTarget;
        }
        if (root is Panel panel)
        {
            foreach (UIElement child in panel.Children)
            {
                if (FindSettingsSearchTarget(child, headerKey, visited) is { } target)
                {
                    return target;
                }
            }
        }
        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < childCount; index++)
        {
            if (FindSettingsSearchTarget(VisualTreeHelper.GetChild(root, index), headerKey, visited) is { } target)
            {
                return target;
            }
        }
        return null;
    }
}
