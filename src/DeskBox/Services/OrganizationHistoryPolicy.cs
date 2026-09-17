using DeskBox.Models;

namespace DeskBox.Services;

/// <summary>
/// Bounds how much undo receipt data <see cref="AppSettings.RecentOrganizationHistory"/>
/// may retain. The history lives inside settings.json, which is re-serialized
/// on every debounced save, so a single unbounded multi-thousand-file batch
/// permanently bloats the file on disk and the object graph rebuilt at every
/// startup. Oversized batches keep their history entry as a summary — real
/// item count, no receipts, never partially undoable.
/// </summary>
/// <remarks>
/// Compaction is only safe once no recovery journal can still reference the
/// receipts: in the desktop organization flow <c>OrganizationHistory.Items</c>
/// doubles as the durable commit evidence that tells
/// <c>RecoverPendingAsync</c> which moves already committed, so receipts are
/// dropped strictly after the journal is cleared, never before the commit
/// save.
/// </remarks>
public static class OrganizationHistoryPolicy
{
    /// <summary>
    /// Maximum receipts a single entry may retain while staying undoable.
    /// Batches above the limit keep only their summary: undo receipts are
    /// all-or-nothing, so a truncated receipt list must never keep
    /// <c>CanUndo=true</c>.
    /// </summary>
    public const int MaxUndoReceiptItemsPerEntry = 500;

    /// <summary>
    /// Maximum receipts retained across the whole history. When the budget
    /// is exceeded the oldest entries are downgraded to summaries first;
    /// history entries are never deleted for budget reasons.
    /// </summary>
    public const int MaxUndoReceiptItemBudget = 2500;

    /// <summary>
    /// Enforces both bounds. The list is newest-first (index 0 is the most
    /// recent entry), matching every append site and the load normalizer.
    /// Entries whose undo lifecycle is still in progress are left untouched:
    /// their receipts are the resume state of an interrupted undo, not
    /// historical data. Returns true when anything changed.
    /// </summary>
    public static bool ApplyRetentionPolicy(List<OrganizationHistoryEntry> history)
    {
        if (history.Count == 0)
        {
            return false;
        }

        bool changed = false;
        foreach (var entry in history)
        {
            if (IsUndoLifecycleActive(entry))
            {
                continue;
            }

            if (entry.UndoReceiptsDiscarded)
            {
                // A retry may have re-added this run's receipts after the
                // transaction already lost older ones; the entry stays a
                // non-undoable summary.
                entry.CanUndo = false;
                if (entry.Items.Count > 0)
                {
                    DowngradeToSummary(entry);
                    changed = true;
                }

                continue;
            }

            if (entry.Items.Count == 0)
            {
                continue;
            }

            if (entry.TotalItemCount < entry.Items.Count)
            {
                entry.TotalItemCount = entry.Items.Count;
                changed = true;
            }

            if (entry.Items.Count > MaxUndoReceiptItemsPerEntry)
            {
                DowngradeToSummary(entry);
                changed = true;
            }
        }

        int totalItems = 0;
        foreach (var entry in history)
        {
            if (!IsUndoLifecycleActive(entry) && !entry.UndoReceiptsDiscarded)
            {
                totalItems += entry.Items.Count;
            }
        }

        for (int i = history.Count - 1; i >= 0 && totalItems > MaxUndoReceiptItemBudget; i--)
        {
            var entry = history[i];
            if (IsUndoLifecycleActive(entry) || entry.UndoReceiptsDiscarded || entry.Items.Count == 0)
            {
                continue;
            }

            totalItems -= entry.Items.Count;
            DowngradeToSummary(entry);
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Merges a retry run into its own previous history entry (same
    /// transaction id). A transaction whose receipts were already discarded
    /// can never regain undo: the merge inherits the discarded marker, keeps
    /// this run's receipts only until the post-journal compaction clears
    /// them, and carries the real total forward.
    /// </summary>
    public static void MergeRetryHistory(OrganizationHistoryEntry history, OrganizationHistoryEntry previous)
    {
        history.Items.InsertRange(0, previous.Items);
        if (previous.UndoReceiptsDiscarded)
        {
            history.UndoReceiptsDiscarded = true;
            history.CanUndo = false;
            history.TotalItemCount = previous.TotalItemCount + history.Items.Count;
        }
    }

    /// <summary>
    /// Strips the receipts while keeping the entry visible in history with
    /// its original item count. A summary entry is never undoable.
    /// </summary>
    public static void DowngradeToSummary(OrganizationHistoryEntry entry)
    {
        entry.TotalItemCount = Math.Max(entry.TotalItemCount, entry.Items.Count);
        entry.Items.Clear();
        entry.CanUndo = false;
        entry.UndoStarted = false;
        entry.UndoReceiptsDiscarded = true;
    }

    /// <summary>
    /// True while an undo is in progress or interrupted: the receipts are
    /// resume state, and an interrupted ManagedDrop undo has no recovery
    /// journal at all, so the check must rely on the persisted entry alone.
    /// A fully undone entry (<see cref="OrganizationHistoryEntry.IsUndone"/>)
    /// is no longer active — its receipts are dead weight.
    /// </summary>
    public static bool IsUndoLifecycleActive(OrganizationHistoryEntry entry)
    {
        return !entry.IsUndone &&
            (entry.UndoStarted || entry.Items.Any(item => item.IsRestored));
    }
}
