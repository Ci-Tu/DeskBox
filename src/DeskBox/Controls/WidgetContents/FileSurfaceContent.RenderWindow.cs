using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DeskBox.Controls.WidgetContents;

public sealed partial class FileSurfaceContent
{
    private ScrollViewer? _gridRenderWindowScrollViewer;
    private ScrollViewer? _listRenderWindowScrollViewer;

    /// <summary>
    /// Grows the view model's render window as the user approaches the end of
    /// the rendered prefix, so a folder with thousands of items scrolls
    /// continuously without ever laying out its full contents at once.
    /// </summary>
    private void RegisterRenderWindowScrollTracking()
    {
        ItemsGrid.Loaded += ItemsView_LoadedForRenderWindow;
        ItemsList.Loaded += ItemsView_LoadedForRenderWindow;
        ItemsGrid.LayoutUpdated += ItemsView_LayoutUpdatedForRenderWindow;
        ItemsList.LayoutUpdated += ItemsView_LayoutUpdatedForRenderWindow;
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
            StoreRenderWindowScrollViewer(itemsView, scrollViewer);
            scrollViewer.ViewChanged -= ItemsView_ViewChangedForRenderWindow;
            scrollViewer.ViewChanged += ItemsView_ViewChangedForRenderWindow;
            TryGrowRenderWindowToFillViewport(scrollViewer);
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

    /// <summary>
    /// A rendered prefix that fits entirely inside the viewport has no
    /// overflow to scroll, so <see cref="ScrollViewer.ViewChanged"/> never
    /// fires and the window would stay stuck at its initial size. After every
    /// layout pass (initial load, item changes, viewport resizes) grow while
    /// the prefix still does not overflow.
    /// </summary>
    private void ItemsView_LayoutUpdatedForRenderWindow(object? sender, object e)
    {
        if (_isDisposed || sender is not ListViewBase itemsView)
        {
            return;
        }

        TryGrowRenderWindowToFillViewport(GetRenderWindowScrollViewer(itemsView));
    }

    private void TryGrowRenderWindowToFillViewport(ScrollViewer? scrollViewer)
    {
        if (_isDisposed || scrollViewer is null || !ViewModel.CanGrowRenderWindow)
        {
            return;
        }

        if (scrollViewer.ViewportHeight > 0 &&
            scrollViewer.ExtentHeight <= scrollViewer.ViewportHeight)
        {
            ViewModel.GrowRenderWindow();
        }
    }

    private void StoreRenderWindowScrollViewer(ListViewBase itemsView, ScrollViewer scrollViewer)
    {
        if (ReferenceEquals(itemsView, ItemsGrid))
        {
            _gridRenderWindowScrollViewer = scrollViewer;
        }
        else if (ReferenceEquals(itemsView, ItemsList))
        {
            _listRenderWindowScrollViewer = scrollViewer;
        }
    }

    private ScrollViewer? GetRenderWindowScrollViewer(ListViewBase itemsView) =>
        ReferenceEquals(itemsView, ItemsGrid) ? _gridRenderWindowScrollViewer :
        ReferenceEquals(itemsView, ItemsList) ? _listRenderWindowScrollViewer :
        FindDescendantScrollViewer(itemsView);

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
