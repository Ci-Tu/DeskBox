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
    /// Returns true when anything changed.
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
            totalItems += entry.Items.Count;
        }

        for (int i = history.Count - 1; i >= 0 && totalItems > MaxUndoReceiptItemBudget; i--)
        {
            if (history[i].Items.Count == 0)
            {
                continue;
            }

            totalItems -= history[i].Items.Count;
            DowngradeToSummary(history[i]);
            changed = true;
        }

        return changed;
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
    }
}
