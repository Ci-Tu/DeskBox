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
    public string GetWidgetCollapseBehaviorDisplayName(string behavior)
    {
        return WidgetCollapseBehaviorNames.Normalize(behavior) switch
        {
            WidgetCollapseBehavior.Expanded => _localizationService.T("Settings.CollapseBehavior.Expanded"),
            WidgetCollapseBehavior.Smart => _localizationService.T("Settings.CollapseBehavior.Smart"),
            _ => _localizationService.T("Settings.CollapseBehavior.Click")
        };
    }

    public string GetHoverButtonActionDisplayName(string action)
    {
        return action switch
        {
            SettingsService.WidgetHoverActionLockPosition => _localizationService.T("Settings.HoverButtonActions.LockPosition"),
            SettingsService.WidgetHoverActionLockSize => _localizationService.T("Settings.HoverButtonActions.LockSize"),
            SettingsService.WidgetHoverActionAdd => _localizationService.T("Settings.HoverButtonActions.Add"),
            SettingsService.WidgetHoverActionMore => _localizationService.T("Settings.HoverButtonActions.More"),
            SettingsService.WidgetHoverActionDelete => _localizationService.T("Settings.HoverButtonActions.Delete"),
            _ => action
        };
    }

    public string GetWidgetTabStyleDisplayName(string style)
    {
        return SettingsService.NormalizeWidgetTabStyle(style) switch
        {
            SettingsService.WidgetTabStyleButton => _localizationService.T("Settings.WidgetTabStyle.Button"),
            _ => _localizationService.T("Settings.WidgetTabStyle.Pivot")
        };
    }

    public string GetTodoNewTaskPositionDisplayName(string position)
    {
        return NormalizeTodoNewTaskPosition(position) switch
        {
            SettingsService.TodoNewTaskPositionBottom => _localizationService.T("Settings.Todo.NewTaskPosition.Bottom"),
            _ => _localizationService.T("Settings.Todo.NewTaskPosition.Top")
        };
    }

    public string GetTodoLayoutModeDisplayName(string mode)
    {
        return SettingsService.NormalizeTodoLayoutMode(mode) switch
        {
            SettingsService.TodoLayoutModeSinglePane =>
                _localizationService.T("Settings.Todo.LayoutMode.SinglePane"),
            SettingsService.TodoLayoutModeDualPane =>
                _localizationService.T("Settings.Todo.LayoutMode.DualPane"),
            _ => _localizationService.T("Settings.Todo.LayoutMode.Auto")
        };
    }

    public string GetTodoDefaultFilterDisplayName(string filter)
    {
        return NormalizeTodoDefaultFilter(filter) switch
        {
            SettingsService.TodoDefaultFilterActive => _localizationService.T("Settings.Todo.DefaultFilter.Active"),
            SettingsService.TodoDefaultFilterToday => _localizationService.T("Settings.Todo.DefaultFilter.Today"),
            SettingsService.TodoDefaultFilterThisWeek => _localizationService.T("Settings.Todo.DefaultFilter.ThisWeek"),
            SettingsService.TodoDefaultFilterThisMonth => _localizationService.T("Settings.Todo.DefaultFilter.ThisMonth"),
            SettingsService.TodoDefaultFilterImportant => _localizationService.T("Settings.Todo.DefaultFilter.Important"),
            SettingsService.TodoDefaultFilterCompleted => _localizationService.T("Settings.Todo.DefaultFilter.Completed"),
            _ => _localizationService.T("Settings.Todo.DefaultFilter.All")
        };
    }

    public string GetTodoReminderOffsetDisplayName(int minutes)
    {
        return SettingsService.NormalizeTodoReminderOffsetMinutes(minutes) switch
        {
            0 => _localizationService.T("Settings.Todo.ReminderOffset.AtDueTime"),
            60 => _localizationService.T("Settings.Todo.ReminderOffset.OneHour"),
            1440 => _localizationService.T("Settings.Todo.ReminderOffset.OneDay"),
            var value => _localizationService.Format("Settings.Todo.ReminderOffset.Minutes", value)
        };
    }

    public string GetLanguageDisplayName(string language)
    {
        return _localizationService.GetLanguageDisplayName(language);
    }
}
