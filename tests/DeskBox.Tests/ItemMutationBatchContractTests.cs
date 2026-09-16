namespace DeskBox.Tests;

/// <summary>
/// Source contract for the batch import mutation scope. WidgetViewModel
/// needs a live dispatcher, so the lifecycle itself is pinned at the source:
/// both bulk import loops must open a scope, every per-item derived reaction
/// (normalization, manual-order persistence, hydration, render-window and
/// stack rebuild queues) must defer while one is active, and the scope must
/// finalize each of them exactly once, in an order where normalization and
/// persistence observe the settled list before anything rebuilds from it.
/// </summary>
public sealed class ItemMutationBatchContractTests
{
    [Fact]
    public void Scope_Sequence_DefersReactionsThenFinalizesEachOnce()
    {
        string batch = File.ReadAllText(TestPaths.FromRepository(
            "src/DeskBox/ViewModels/WidgetViewModel.ItemMutationBatch.cs"));

        Assert.Contains("internal IDisposable EnterItemMutationScope()", batch, StringComparison.Ordinal);
        Assert.Contains("MarkItemMutationBatchDirty() => _itemMutationBatchDirty = true;", batch, StringComparison.Ordinal);

        // Finalization must run every deferred reaction exactly once...
        foreach (string reaction in new[]
                 {
                     "owner.NormalizeSortOrder();",
                     "owner.PersistManualOrderSnapshotIfChanged();",
                     "owner.QueueStackDisplayRebuild();",
                     "owner.QueueRenderWindowReconcile();",
                     "owner.StartItemHydration();"
                 })
        {
            Assert.Contains(reaction, batch, StringComparison.Ordinal);
        }

        // ...in an order where normalization and persistence settle the
        // list before anything rebuilds a projection from it, and hydration
        // starts against the freshly reconciled window.
        Assert.True(
            batch.IndexOf("owner.NormalizeSortOrder();", StringComparison.Ordinal) <
            batch.IndexOf("owner.QueueStackDisplayRebuild();", StringComparison.Ordinal),
            "normalize must precede the stack rebuild");
        Assert.True(
            batch.IndexOf("owner.QueueRenderWindowReconcile();", StringComparison.Ordinal) <
            batch.IndexOf("owner.StartItemHydration();", StringComparison.Ordinal),
            "the render reconcile must precede hydration");
        // Nested scopes finalize only when the last one closes.
        Assert.Contains(
            "owner._itemMutationBatchDepth > 0 || !owner._itemMutationBatchDirty",
            batch,
            StringComparison.Ordinal);
    }

    [Fact]
    public void UpsertAndRemoval_DeferDerivedWorkInsideABatch()
    {
        string watchers = File.ReadAllText(TestPaths.FromRepository(
            "src/DeskBox/ViewModels/WidgetViewModel.SortingAndWatchers.cs"))
            .Replace("\r\n", "\n");

        // Both UpsertFolderItemAsync branches route through the gated helper.
        Assert.Equal(2, CountOccurrences(watchers, "FinishItemUpsert();"));
        // The old per-upsert tails ended with hydration followed by the
        // branch return; only the helper may carry that sequence now, and it
        // never returns true.
        Assert.DoesNotContain(
            "StartItemHydration();\n            return true;",
            watchers,
            StringComparison.Ordinal);

        // The helper defers while a batch is active...
        int helper = watchers.IndexOf(
            "private void FinishItemUpsert()",
            StringComparison.Ordinal);
        int gate = watchers.IndexOf(
            "if (_itemMutationBatchDepth > 0)",
            helper,
            StringComparison.Ordinal);
        int deferredReturn = watchers.IndexOf(
            "MarkItemMutationBatchDirty();",
            gate,
            StringComparison.Ordinal);
        int normalize = watchers.IndexOf(
            "NormalizeSortOrder();",
            gate,
            StringComparison.Ordinal);
        Assert.True(gate > helper && deferredReturn > gate && normalize > deferredReturn,
            "FinishItemUpsert must gate on the batch depth before its fallback tail");

        // ...and RemoveItemByPath defers the same way before its own tail.
        int removal = watchers.IndexOf(
            "private void RemoveItemByPath",
            StringComparison.Ordinal);
        int removalGate = watchers.IndexOf(
            "if (_itemMutationBatchDepth > 0)",
            removal,
            StringComparison.Ordinal);
        int removalDirty = watchers.IndexOf(
            "MarkItemMutationBatchDirty();",
            removalGate,
            StringComparison.Ordinal);
        int removalNormalize = watchers.IndexOf(
            "NormalizeSortOrder();",
            removalGate,
            StringComparison.Ordinal);
        Assert.True(removalGate > removal && removalDirty > removalGate &&
            removalNormalize > removalDirty,
            "RemoveItemByPath must gate on the batch depth before its tail");
    }

    [Fact]
    public void QueuedReactions_GateOnTheBatchAndMarkDirty()
    {
        string windowing = File.ReadAllText(TestPaths.FromRepository(
            "src/DeskBox/ViewModels/WidgetViewModel.Windowing.cs"))
            .Replace("\r\n", "\n");
        string stacks = File.ReadAllText(TestPaths.FromRepository(
            "src/DeskBox/ViewModels/WidgetViewModel.Stacks.cs"))
            .Replace("\r\n", "\n");

        // The render-window queue defers inside a batch, before its own
        // coalescing flag: the finalization must be able to enqueue for real.
        int queue = windowing.IndexOf(
            "private void QueueRenderWindowReconcile()",
            StringComparison.Ordinal);
        int disposedCheck = windowing.IndexOf(
            "if (_isDisposed)",
            queue,
            StringComparison.Ordinal);
        int gate = windowing.IndexOf(
            "if (_itemMutationBatchDepth > 0)",
            queue,
            StringComparison.Ordinal);
        int queuedFlag = windowing.IndexOf(
            "if (_renderWindowReconcileQueued)",
            queue,
            StringComparison.Ordinal);
        Assert.True(
            disposedCheck > queue && gate > disposedCheck && queuedFlag > gate,
            "the batch gate must sit between the disposed check and the coalescing flag");
        Assert.Contains(
            "reconciles would re-mirror the prefix once per dispatcher pass",
            windowing,
            StringComparison.Ordinal);

        // The stack queue keeps its staleness flag set during the batch so
        // mid-batch readers know the projection is behind, then defers the
        // enqueue itself.
        int stackQueue = stacks.IndexOf(
            "private void QueueStackDisplayRebuild()",
            StringComparison.Ordinal);
        int staleness = stacks.IndexOf(
            "_hasBuiltStackDisplay = false;",
            stackQueue,
            StringComparison.Ordinal);
        int stackQueuedFlag = stacks.IndexOf(
            "if (_stackRebuildQueued)",
            stackQueue,
            StringComparison.Ordinal);
        int stackGate = stacks.IndexOf(
            "if (_itemMutationBatchDepth > 0)",
            stackQueue,
            StringComparison.Ordinal);
        int stackEnqueue = stacks.IndexOf(
            "_stackRebuildQueued = true;",
            stackQueue,
            StringComparison.Ordinal);
        Assert.True(
            staleness > stackQueue && stackQueuedFlag > staleness &&
            stackGate > stackQueuedFlag && stackEnqueue > stackGate,
            "the stack staleness flag and queued-flag check must precede the batch gate and the enqueue");
    }

    [Fact]
    public void BulkImportLoops_OpenExactlyOneScopeEach()
    {
        string operations = File.ReadAllText(TestPaths.FromRepository(
            "src/DeskBox/ViewModels/WidgetViewModel.Operations.cs"));

        Assert.Equal(2, CountOccurrences(operations, "EnterItemMutationScope()"));
        // The transfer-results batch must finalize even when an upsert
        // throws: scope disposal sits in a finally alongside the perf log.
        Assert.Contains(
            "IDisposable batchScope = EnterItemMutationScope();",
            operations,
            StringComparison.Ordinal);
        Assert.Contains("batchScope.Dispose();", operations, StringComparison.Ordinal);
        Assert.Contains(
            "[OrganizerPerf] importBatch attempted=",
            operations,
            StringComparison.Ordinal);
    }

    private static int CountOccurrences(string text, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
