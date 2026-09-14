using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DeskBox.Controls.WidgetContents;

public sealed partial class FileSurfaceContent
{
    /// <summary>
    /// Grows the view model's render window as the user approaches the end of
    /// the rendered prefix, so a folder with thousands of items scrolls
    /// continuously without ever laying out its full contents at once.
    /// </summary>
    private void RegisterRenderWindowScrollTracking()
    {
        ItemsGrid.Loaded += ItemsView_LoadedForRenderWindow;
        ItemsList.Loaded += ItemsView_LoadedForRenderWindow;
    }

    private void ItemsView_LoadedForRenderWindow(object sender, RoutedEventArgs e)
    {
        HookRenderWindowScrollViewer(sender as ListViewBase, retry: true);
    }

    private void HookRenderWindowScrollViewer(ListViewBase? itemsView, bool retry)
    {
        if (_isDisposed || itemsView is null)
        {
            return;
        }

        if (FindDescendantScrollViewer(itemsView) is { } scrollViewer)
        {
            scrollViewer.ViewChanged -= ItemsView_ViewChangedForRenderWindow;
            scrollViewer.ViewChanged += ItemsView_ViewChangedForRenderWindow;
            return;
        }

        if (retry)
        {
            // The templated ScrollViewer may not exist during the first
            // Loaded pass; retry once layout has produced it.
            itemsView.LayoutUpdated += RetryRenderWindowScrollHook;
        }
    }

    private void RetryRenderWindowScrollHook(object? sender, object e)
    {
        if (sender is ListViewBase itemsView)
        {
            itemsView.LayoutUpdated -= RetryRenderWindowScrollHook;
        }

        HookRenderWindowScrollViewer(sender as ListViewBase, retry: false);
    }

    private void ItemsView_ViewChangedForRenderWindow(
        object? sender,
        ScrollViewerViewChangedEventArgs e)
    {
        if (_isDisposed || sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        // Grow while unrendered content remains and the user is within two
        // viewports of the rendered end, so scrolling never hits a wall.
        if (scrollViewer.VerticalOffset + (scrollViewer.ViewportHeight * 2) >=
            scrollViewer.ExtentHeight)
        {
            ViewModel.GrowRenderWindow();
        }
    }

    private static ScrollViewer? FindDescendantScrollViewer(DependencyObject parent)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int index = 0; index < count; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }

            if (FindDescendantScrollViewer(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }
}
