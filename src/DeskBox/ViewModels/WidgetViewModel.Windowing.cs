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
    /// Expands the window in chunks until <paramref name="item"/> is
    /// rendered (used before scrolling an item into view), without paying
    /// the full-folder layout unless the item actually sits that deep.
    /// </summary>
    internal void EnsureItemRendered(WidgetItem item)
    {
        if (_isDisposed || RenderedItems.Contains(item) || !CanGrowRenderWindow)
        {
            return;
        }

        while (!RenderedItems.Contains(item) && CanGrowRenderWindow)
        {
            _renderWindowCount = Math.Min(
                VisibleItemCount,
                _renderWindowCount + RenderWindowGrowChunk);
        }

        ReconcileRenderWindow();
        if (!UsesStackProjection)
        {
            StartItemHydration();
        }
    }

    /// <summary>
    /// Mirrors the current VisibleItems prefix into <see cref="RenderedItems"/>
    /// in place (move/insert/remove by index, never a Reset), mirroring the
    /// reconcile strategy the stack projection already uses so container
    /// realization and selection survive content changes.
    /// </summary>
    private void ReconcileRenderWindow()
    {
        int targetCount = Math.Min(_renderWindowCount, VisibleItemCount);
        var desired = new List<WidgetItem>(targetCount);
        int collected = 0;
        foreach (WidgetItem item in VisibleItems)
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
            if (targetIndex < RenderedItems.Count &&
                ReferenceEquals(RenderedItems[targetIndex], desiredItem))
            {
                continue;
            }

            int existingIndex = IndexOfReference(
                RenderedItems,
                desiredItem,
                targetIndex + 1);
            if (existingIndex >= 0)
            {
                RenderedItems.Move(existingIndex, targetIndex);
            }
            else
            {
                RenderedItems.Insert(
                    Math.Min(targetIndex, RenderedItems.Count),
                    desiredItem);
            }
        }

        while (RenderedItems.Count > desired.Count)
        {
            RenderedItems.RemoveAt(RenderedItems.Count - 1);
        }
    }
}
