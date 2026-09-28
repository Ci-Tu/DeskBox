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
    partial void OnTodoShowTabBarChanged(bool value)
    {
        ApplyTodoTabBarVisibility(value);
    }

    partial void OnTodoShowAllTabChanged(bool value)
    {
        ApplyTodoTabVisibility(SettingsService.TodoDefaultFilterAll, value);
    }

    partial void OnTodoShowActiveTabChanged(bool value)
    {
        ApplyTodoTabVisibility(SettingsService.TodoDefaultFilterActive, value);
    }

    partial void OnTodoShowTodayTabChanged(bool value)
    {
        ApplyTodoTabVisibility(SettingsService.TodoDefaultFilterToday, value);
    }

    partial void OnTodoShowThisWeekTabChanged(bool value)
    {
        ApplyTodoTabVisibility(SettingsService.TodoDefaultFilterThisWeek, value);
    }

    partial void OnTodoShowThisMonthTabChanged(bool value)
    {
        ApplyTodoTabVisibility(SettingsService.TodoDefaultFilterThisMonth, value);
    }

    partial void OnTodoShowImportantTabChanged(bool value)
    {
        ApplyTodoTabVisibility(SettingsService.TodoDefaultFilterImportant, value);
    }

    partial void OnTodoShowCompletedTabChanged(bool value)
    {
        ApplyTodoTabVisibility(SettingsService.TodoDefaultFilterCompleted, value);
    }

    partial void OnTodoUseWideDetailPaneChanged(bool value)
    {
        if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
        {
            return;
        }

        _todoSettings.SetLegacyWideDetailPane(value);
    }

    partial void OnTodoAutoSelectFirstInWideLayoutChanged(bool value)
    {
        OnPropertyChanged(nameof(TodoLayoutSummaryText));
        if (_isRestoringDefaults || _isApplyingSettingsSnapshot)
        {
            return;
        }

        _todoSettings.AutoSelectFirstInWideLayout = value;
    }

    partial void OnTodoShowCompletedTasksChanged(bool value)
    {
        if (_isRestoringDefaults || _isApplyingSettingsSnapshot) return;
        _todoSettings.ShowCompletedTasks = value;
        SyncTodoDisplayFacade();
    }

    partial void OnTodoShowFooterStatsChanged(bool value)
    {
        if (_isRestoringDefaults || _isApplyingSettingsSnapshot) return;
        _todoSettings.ShowFooterStats = value;
        SyncTodoDisplayFacade();
    }

    partial void OnTodoShowClearCompletedButtonChanged(bool value)
    {
        if (_isRestoringDefaults || _isApplyingSettingsSnapshot) return;
        _todoSettings.ShowClearCompletedButton = value;
        SyncTodoDisplayFacade();
    }
}
