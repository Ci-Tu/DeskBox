using System.Collections.ObjectModel;
using DeskBox.Models;
using DeskBox.ViewModels;

namespace DeskBox.Tests;

/// <summary>
/// Real behavior coverage for the render window. A reveal must grow the
/// window just far enough to cover the target item — never to the whole
/// folder. The original loop-based implementation expanded to the full item
/// count whenever the target sat outside the rendered prefix, which is the
/// measured ~50 s UI hang for large folders all over again.
/// </summary>
public sealed class RenderWindowBehaviorTests
{
    [Theory]
    [InlineData(0, 30, 1000, 200)]      // first item: aligned chunk boundary
    [InlineData(49, 30, 1000, 200)]     // just past the window: next chunk boundary
    [InlineData(199, 30, 1000, 200)]
    [InlineData(200, 30, 1000, 400)]    // exactly one past the boundary: two chunks
    [InlineData(949, 30, 1000, 1000)]   // deep item: clamped to the visible count
    [InlineData(949, 30, 2000, 1000)]
    [InlineData(1999, 1999, 2000, 2000)]
    [InlineData(-1, 30, 1000, 30)]      // not in the projection: unchanged
    public void ComputeRenderWindowTargetCount_AlignsToChunksAndClamps(
        int itemIndex,
        int currentCount,
        int visibleCount,
        int expected)
    {
        Assert.Equal(
            expected,
            WidgetViewModel.ComputeRenderWindowTargetCount(itemIndex, currentCount, visibleCount));
    }

    [Fact]
    public void RevealDeepItem_GrowsTheWindowJustEnoughToCoverIt()
    {
        List<WidgetItem> visible = CreateItems(5000);
        var rendered = new ObservableCollection<WidgetItem>(visible.Take(30));

        int windowCount = WidgetViewModel.ReconcileRenderWindowPrefix(
            visible,
            rendered,
            WidgetViewModel.ComputeRenderWindowTargetCount(949, 30, 5000),
            visible.Count);

        Assert.Equal(1000, windowCount);
        Assert.Equal(1000, rendered.Count);
        Assert.Same(visible[949], rendered[949]);
        Assert.Contains(visible[949], rendered);
        // The tail beyond the grown window must stay unrealized.
        Assert.DoesNotContain(visible[1500], rendered);
        for (int index = 0; index < rendered.Count; index++)
        {
            Assert.Same(visible[index], rendered[index]);
        }
    }

    [Fact]
    public void RevealShallowItem_KeepsTheFolderLargelyUnrendered()
    {
        List<WidgetItem> visible = CreateItems(5000);
        var rendered = new ObservableCollection<WidgetItem>(visible.Take(30));

        WidgetViewModel.ReconcileRenderWindowPrefix(
            visible,
            rendered,
            WidgetViewModel.ComputeRenderWindowTargetCount(49, 30, 5000),
            visible.Count);

        Assert.Equal(200, rendered.Count);
        Assert.Contains(visible[49], rendered);
        Assert.DoesNotContain(visible[200], rendered);
    }

    [Fact]
    public void SmallFolders_AlwaysRenderInFull()
    {
        List<WidgetItem> visible = CreateItems(250);
        var rendered = new ObservableCollection<WidgetItem>(visible.Take(30));

        int windowCount = WidgetViewModel.ReconcileRenderWindowPrefix(
            visible,
            rendered,
            windowCount: 30,
            visibleItemCount: visible.Count);

        Assert.Equal(250, windowCount);
        Assert.Equal(250, rendered.Count);
    }

    [Fact]
    public void Reconcile_ReordersInPlaceWithoutReset()
    {
        List<WidgetItem> visible = CreateItems(500);
        var rendered = new ObservableCollection<WidgetItem>(visible.Take(200));

        // Simulate a re-sort: swap the first two visible items and reverse a
        // slice well inside the window. The prefix mirror must reuse existing
        // entries (move/insert) so container realization survives.
        (visible[0], visible[1]) = (visible[1], visible[0]);
        visible.Reverse(10, 20);

        WidgetViewModel.ReconcileRenderWindowPrefix(
            visible,
            rendered,
            windowCount: 200,
            visibleItemCount: visible.Count);

        Assert.Equal(200, rendered.Count);
        for (int index = 0; index < rendered.Count; index++)
        {
            Assert.Same(visible[index], rendered[index]);
        }
    }

    [Fact]
    public void Reconcile_TrimsTheTailWhenTheFolderShrinks()
    {
        List<WidgetItem> visible = CreateItems(500);
        var rendered = new ObservableCollection<WidgetItem>(visible.Take(200));

        visible.RemoveRange(100, 400);

        WidgetViewModel.ReconcileRenderWindowPrefix(
            visible,
            rendered,
            windowCount: 200,
            visibleItemCount: visible.Count);

        Assert.Equal(100, rendered.Count);
        Assert.Same(visible[99], rendered[99]);
    }

    private static List<WidgetItem> CreateItems(int count)
    {
        var items = new List<WidgetItem>(count);
        for (int index = 0; index < count; index++)
        {
            items.Add(new WidgetItem { Path = $@"C:\folder\item{index:D5}.txt" });
        }

        return items;
    }
}
