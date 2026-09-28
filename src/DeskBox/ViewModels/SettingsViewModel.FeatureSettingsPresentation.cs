using DeskBox.Models;
using DeskBox.Services;
using Microsoft.UI.Xaml;

namespace DeskBox.ViewModels;

public partial class SettingsViewModel
{
    // The Quick Capture tab-group presentation (visible-tabs summary, the
    // filtered default-view options, the tab-visibility flyout state machine
    // and the tab-style index) lives on the Quick Capture section editor
    // (batch 46); only the Todo twins remain here.

    public IReadOnlyList<SettingsOption> VisibleTodoDefaultFilterOptions =>
        AvailableTodoDefaultFilters
            .Select((value, index) => new { value, index })
            .Where(item => IsTodoTabSelected(item.value))
            .Select(item => new SettingsOption(
                item.value,
                AvailableTodoDefaultFilterDisplayNames[item.index]))
            .ToArray();

    public string TodoVisibleTabsText => JoinSelectedTodoTabs();

    public string TodoTabsSummaryText => TodoShowTabBar
        ? TodoVisibleTabsText
        : _localizationService.T("Settings.Toggle.Off");

    public string TodoLayoutSummaryText => GetTodoLayoutModeDisplayName(SelectedTodoLayoutMode);

    public string TodoContentSummaryText => string.Join(
        " · ",
        GetItemPreviewLineCountDisplayName(TodoItemPreviewLineCount),
        SelectedTodoNewTaskPositionText,
        GetEditorEnterBehaviorDisplayName(TodoEditorEnterBehavior));

    public string TodoReminderSummaryText => TodoReminderEnabled
        ? SelectedTodoReminderOffsetMinutesText
        : _localizationService.T("Settings.Toggle.Off");

    public string TodoFooterDisplaySummaryText
    {
        get
        {
            var selected = new List<string>(2);
            if (TodoShowFooterStats)
            {
                selected.Add(_localizationService.T("Settings.Todo.ShowFooterStats.Title"));
            }

            if (TodoShowClearCompletedButton)
            {
                selected.Add(_localizationService.T("Settings.Todo.ShowClearCompleted.Title"));
            }

            return selected.Count == 0
                ? _localizationService.T("Settings.Toggle.Off")
                : string.Join(" · ", selected);
        }
    }

    public Visibility TodoWideOptionsVisibility =>
        SelectedTodoLayoutMode == SettingsService.TodoLayoutModeSinglePane
            ? Visibility.Collapsed
            : Visibility.Visible;

    public int TodoTabStyleIndex
    {
        get => SelectedTodoTabStyle == SettingsService.WidgetTabStyleButton ? 1 : 0;
        set => SelectedTodoTabStyle = value == 1
            ? SettingsService.WidgetTabStyleButton
            : SettingsService.WidgetTabStylePivot;
    }

    public bool IsTodoTabSelected(string filter) =>
        NormalizeTodoDefaultFilter(filter) switch
        {
            SettingsService.TodoDefaultFilterActive => TodoShowActiveTab,
            SettingsService.TodoDefaultFilterToday => TodoShowTodayTab,
            SettingsService.TodoDefaultFilterThisWeek => TodoShowThisWeekTab,
            SettingsService.TodoDefaultFilterThisMonth => TodoShowThisMonthTab,
            SettingsService.TodoDefaultFilterImportant => TodoShowImportantTab,
            SettingsService.TodoDefaultFilterCompleted => TodoShowCompletedTab,
            _ => TodoShowAllTab
        };

    public bool CanToggleTodoTab(string filter) =>
        !IsTodoTabSelected(filter) || CountSelectedTodoTabs() > 1;

    public void ToggleTodoTab(string filter)
    {
        bool selected = IsTodoTabSelected(filter);
        if (selected && !CanToggleTodoTab(filter))
        {
            return;
        }

        switch (NormalizeTodoDefaultFilter(filter))
        {
            case SettingsService.TodoDefaultFilterActive:
                TodoShowActiveTab = !selected;
                break;
            case SettingsService.TodoDefaultFilterToday:
                TodoShowTodayTab = !selected;
                break;
            case SettingsService.TodoDefaultFilterThisWeek:
                TodoShowThisWeekTab = !selected;
                break;
            case SettingsService.TodoDefaultFilterThisMonth:
                TodoShowThisMonthTab = !selected;
                break;
            case SettingsService.TodoDefaultFilterImportant:
                TodoShowImportantTab = !selected;
                break;
            case SettingsService.TodoDefaultFilterCompleted:
                TodoShowCompletedTab = !selected;
                break;
            default:
                TodoShowAllTab = !selected;
                break;
        }
    }

    public string GetTodoTabDisplayName(string filter) =>
        GetTodoDefaultFilterDisplayName(filter);

    public bool IsTodoFooterDisplayOptionSelected(string option) => option switch
    {
        "Stats" => TodoShowFooterStats,
        "ClearCompleted" => TodoShowClearCompletedButton,
        _ => false
    };

    public void ToggleTodoFooterDisplayOption(string option)
    {
        switch (option)
        {
            case "Stats":
                TodoShowFooterStats = !TodoShowFooterStats;
                break;
            case "ClearCompleted":
                TodoShowClearCompletedButton = !TodoShowClearCompletedButton;
                break;
        }
    }

    public string GetTodoFooterDisplayOptionName(string option) => option switch
    {
        "Stats" => _localizationService.T("Settings.Todo.ShowFooterStats.Title"),
        "ClearCompleted" => _localizationService.T("Settings.Todo.ShowClearCompleted.Title"),
        _ => string.Empty
    };

    private string GetItemPreviewLineCountDisplayName(int lineCount) => lineCount == 1
        ? _localizationService.T("Settings.ContentEditor.PreviewLines.Option.Single")
        : _localizationService.Format(
            "Settings.ContentEditor.PreviewLines.Option.Multiple",
            lineCount);

    private string JoinSelectedTodoTabs() => string.Join(
        " · ",
        AvailableTodoDefaultFilters
            .Where(IsTodoTabSelected)
            .Select(GetTodoTabDisplayName));

    private int CountSelectedTodoTabs() =>
        AvailableTodoDefaultFilters.Count(IsTodoTabSelected);

    private void RefreshTodoTabsPresentation()
    {
        OnPropertyChanged(nameof(TodoVisibleTabsText));
        OnPropertyChanged(nameof(TodoTabsSummaryText));
        OnPropertyChanged(nameof(VisibleTodoDefaultFilterOptions));
    }

    private void RefreshTodoContentPresentation()
    {
        OnPropertyChanged(nameof(TodoContentSummaryText));
    }
}
