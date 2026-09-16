using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using DeskBox.Models;

namespace DeskBox.ViewModels;

/// <summary>
/// Render-window projection for large folders. The XAML items controls bind
/// <see cref="RenderedItems"/>, a prefix of <see cref="WidgetViewModel.VisibleItems"/>.
/// Folders at or below the activation threshold render everything (identical
/// to the previous full-list binding); larger folders render incrementally so
/// opening them cannot lay out thousands of tiles at once. Metadata hydration
/// follows the same prefix, so icons, folder counts, shortcut targets, and
/// shell kinds are only resolved for items the user can actually see.
/// </summary>
public partial class WidgetViewModel
{
    private const int RenderWindowActivationThreshold = 300;
    private const int RenderWindowInitialSize = 30;
    private const int RenderWindowGrowChunk = 200;

    public ObservableCollection<WidgetItem> RenderedItems { get; } = [];

    private int _renderWindowCount = RenderWindowInitialSize;
    private bool _renderWindowReconcileQueued;

    private int VisibleItemCount => UsesStackProjection
        ? _stackDisplayItems.Count
        : Items.Count;

    internal bool CanGrowRenderWindow => _renderWindowCount < VisibleItemCount;

    /// <summary>
    /// Items eligible for metadata hydration (icons, folder counts, shortcut
    /// targets, shell kinds). Stack grouping reads <c>ShellKind</c> from every
    /// item, so the full list stays eligible while stacks are enabled;
    /// otherwise a windowed folder hydrates only its rendered prefix and
    /// picks the remaining items up as the window grows.
    /// </summary>
    internal IEnumerable<WidgetItem> HydrationUniverseItems => UsesStackProjection
        ? Items
        : RenderedItems;

    private void AttachRenderWindowTracking()
    {
        Items.CollectionChanged += OnItemsChangedForRenderWindow;
        _stackDisplayItems.CollectionChanged += OnItemsChangedForRenderWindow;
    }

    private void OnItemsChangedForRenderWindow(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        QueueRenderWindowReconcile();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(VisibleItems))
        {
            QueueRenderWindowReconcile();
        }
    }

    private void QueueRenderWindowReconcile()
    {
        if (_isDisposed || _renderWindowReconcileQueued)
        {
            return;
        }

        _renderWindowReconcileQueued = true;
        _dispatcherQueue.TryEnqueue(() =>
        {
            _renderWindowReconcileQueued = false;
            if (_isDisposed)
            {
                return;
            }

            ReconcileRenderWindow();
        });
    }

    /// <summary>
    /// Called when the browsed folder itself changes (navigating into or out
    /// of a folder) so a window grown in a previous folder does not carry
    /// over. Folder refreshes intentionally keep the grown window.
    /// </summary>
    internal void ResetRenderWindow()
    {
        _renderWindowCount = RenderWindowInitialSize;
        ReconcileRenderWindow();
    }

    /// <summary>
    /// Raises the window so the rendered prefix covers a viewport-sized
    /// page. The fixed initial size (30) can sit well below what a large
    /// widget viewport shows, and the extent-based growth fallback only
    /// reacts after a full layout pass — a prefix that never overflows
    /// leaves unrendered items unreachable with no scrollbar. The caller
    /// computes the page size from its real viewport and item dimensions;
    /// this only clamps it to the visible item count.
    /// </summary>
    internal void EnsureRenderWindowCoversViewport(int minimumCount)
    {
        if (_isDisposed || minimumCount <= _renderWindowCount)
        {
            return;
        }

        _renderWindowCount = Math.Min(minimumCount, VisibleItemCount);
        ReconcileRenderWindow();
        if (!UsesStackProjection)
        {
            StartItemHydration();
        }
    }

    /// <summary>
    /// The smallest render window that fills a viewport: enough columns for
    /// the width, enough rows for the height plus two buffer rows so the
    /// scrollbar has extent to grow from. Pure so tests can pin it.
    /// </summary>
    internal static int ComputeViewportRenderMinimum(
        double viewportWidth,
        double viewportHeight,
        double itemWidth,
        double itemHeight,
        int bufferRows)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0 ||
            itemWidth <= 0 || itemHeight <= 0)
        {
            return RenderWindowInitialSize;
        }

        int columns = (int)Math.Ceiling(viewportWidth / itemWidth);
        int rows = (int)Math.Ceiling(viewportHeight / itemHeight) + bufferRows;
        return Math.Max(RenderWindowInitialSize, columns * Math.Max(1, rows));
    }

    internal void GrowRenderWindow(int chunk = RenderWindowGrowChunk)
    {
        if (_isDisposed || !CanGrowRenderWindow)
        {
            return;
        }

        _renderWindowCount = Math.Min(
            VisibleItemCount,
            _renderWindowCount + Math.Max(1, chunk));
        ReconcileRenderWindow();
        if (!UsesStackProjection)
        {
            // Hydration skips items that already resolved, so this pass only
            // picks up the newly rendered prefix members.
            StartItemHydration();
        }
    }

    /// <summary>
    /// Expands the window just far enough to cover <paramref name="item"/>
    /// (used before scrolling an item into view), aligned up to a grow chunk.
    /// The window size, not the whole folder, bounds the layout cost: a reveal
    /// into a thousand-item folder must not realize every tile.
    /// </summary>
    internal void EnsureItemRendered(WidgetItem item)
    {
        if (_isDisposed || RenderedItems.Contains(item) || !CanGrowRenderWindow)
        {
            return;
        }

        int targetIndex = IndexInVisibleItems(item);
        if (targetIndex < 0)
        {
            // Reachable in the stack projection when the item is folded into a
            // collapsed stack; there is nothing to render until it expands.
            return;
        }

        _renderWindowCount = ComputeRenderWindowTargetCount(
            targetIndex,
            _renderWindowCount,
            VisibleItemCount);
        ReconcileRenderWindow();
        if (!UsesStackProjection)
        {
            StartItemHydration();
        }
    }

    private int IndexInVisibleItems(WidgetItem item)
    {
        int index = 0;
        foreach (WidgetItem candidate in VisibleItems)
        {
            if (ReferenceEquals(candidate, item))
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    /// <summary>
    /// Window size that just covers <paramref name="itemIndex"/>, aligned up
    /// to a grow chunk so nearby reveals do not re-trigger growth. Unknown
    /// indices (<c>-1</c>) keep the current window.
    /// </summary>
    internal static int ComputeRenderWindowTargetCount(
        int itemIndex,
        int currentCount,
        int visibleCount)
    {
        if (itemIndex < 0)
        {
            return currentCount;
        }

        int requiredCount = itemIndex + 1;
        int aligned =
            ((requiredCount + RenderWindowGrowChunk - 1) / RenderWindowGrowChunk) *
            RenderWindowGrowChunk;
        return Math.Min(
            visibleCount,
            Math.Max(currentCount + 1, aligned));
    }

    /// <summary>
    /// Mirrors the current VisibleItems prefix into <see cref="RenderedItems"/>
    /// in place (move/insert/remove by index, never a Reset), mirroring the
    /// reconcile strategy the stack projection already uses so container
    /// realization and selection survive content changes. Returns the window
    /// size that actually applied.
    /// </summary>
    private void ReconcileRenderWindow()
    {
        _renderWindowCount = ReconcileRenderWindowPrefix(
            VisibleItems,
            RenderedItems,
            _renderWindowCount,
            VisibleItemCount);
    }

    /// <summary>
    /// Pure prefix mirror shared with behavior tests: folders at or below the
    /// activation threshold always render in full; larger folders render the
    /// first <paramref name="windowCount"/> visible items.
    /// </summary>
    internal static int ReconcileRenderWindowPrefix(
        IEnumerable<WidgetItem> visibleItems,
        ObservableCollection<WidgetItem> renderedItems,
        int windowCount,
        int visibleItemCount)
    {
        if (visibleItemCount <= RenderWindowActivationThreshold)
        {
            // Folders within the activation threshold always render in full;
            // the incremental window only applies above it. Without this the
            // initial prefix caps every folder at RenderWindowInitialSize.
            windowCount = visibleItemCount;
        }

        int targetCount = Math.Min(windowCount, visibleItemCount);
        var desired = new List<WidgetItem>(targetCount);
        int collected = 0;
        foreach (WidgetItem item in visibleItems)
        {
            if (collected >= targetCount)
            {
                break;
            }

            desired.Add(item);
            collected++;
        }

        for (int targetIndex = 0; targetIndex < desired.Count; targetIndex++)
        {
            WidgetItem desiredItem = desired[targetIndex];
            if (targetIndex < renderedItems.Count &&
                ReferenceEquals(renderedItems[targetIndex], desiredItem))
            {
                continue;
            }

            int existingIndex = IndexOfReference(
                renderedItems,
                desiredItem,
                targetIndex + 1);
            if (existingIndex >= 0)
            {
                renderedItems.Move(existingIndex, targetIndex);
            }
            else
            {
                renderedItems.Insert(
                    Math.Min(targetIndex, renderedItems.Count),
                    desiredItem);
            }
        }

        while (renderedItems.Count > desired.Count)
        {
            renderedItems.RemoveAt(renderedItems.Count - 1);
        }

        return windowCount;
    }
}
