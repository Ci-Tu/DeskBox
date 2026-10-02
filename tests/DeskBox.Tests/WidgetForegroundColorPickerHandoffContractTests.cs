namespace DeskBox.Tests;

public sealed class WidgetForegroundColorPickerHandoffContractTests
{
    [Fact]
    public void ForegroundColorPicker_ContentChainHoldsInteractionAcrossMenuTransition()
    {
        string source = File.ReadAllText(TestPaths.FromRepository(
            "src/DeskBox/Views/ContentWidgetWindow.Commands.cs"));

        // The picker item acquires the same handoff the close confirmation
        // chain uses, so a grouped Smart capsule cannot collapse during the
        // dispatcher turn between the menu closing and the picker opening.
        Assert.Contains(
            "pickerHandoff ??= AcquireCloseWidgetFlyoutHandoff();",
            source,
            StringComparison.Ordinal);

        string closedHandler = ExtractSection(
            source,
            "bool showForegroundColorPickerWhenClosed = false;",
            "flyout.Items.Add(rename);");
        Assert.Contains(
            "QueueInteractionGuardedFlyout(",
            closedHandler,
            StringComparison.Ordinal);
        Assert.Contains(
            "BuildWidgetForegroundColorPickerFlyout(),",
            closedHandler,
            StringComparison.Ordinal);
        Assert.Contains(
            "pickerHandoff,",
            closedHandler,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ForegroundColorPicker_QuickCaptureChainHoldsInteractionAcrossMenuTransition()
    {
        string source = File.ReadAllText(TestPaths.FromRepository(
            "src/DeskBox/Views/QuickCaptureWidgetWindow.Menus.cs"));

        Assert.Contains(
            "pickerHandoff ??= AcquireFlyoutHandoff(\"quick-color-picker-handoff\");",
            source,
            StringComparison.Ordinal);

        string closedHandler = ExtractSection(
            source,
            "bool showForegroundColorPickerWhenClosed = false;",
            "flyout.Items.Add(renameItem);");
        Assert.Contains(
            "QueueInteractionGuardedShow(",
            closedHandler,
            StringComparison.Ordinal);
        Assert.Contains(
            "BuildWidgetForegroundColorPickerFlyout(),",
            closedHandler,
            StringComparison.Ordinal);
        Assert.Contains(
            "pickerHandoff,",
            closedHandler,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ForegroundColorPicker_SharedHandoffReleasesOnlyAfterSuccessorAcquiresLease()
    {
        string source = File.ReadAllText(TestPaths.FromRepository(
            "src/DeskBox/Views/WidgetWindowBase.Interaction.cs"));

        string acquire = ExtractSection(
            source,
            "protected IDisposable AcquireFlyoutHandoff(string reason)",
            "protected void QueueInteractionGuardedShow(");
        Assert.Contains("BeginCompactInteraction();", acquire, StringComparison.Ordinal);
        Assert.Contains("BeginWidgetInteraction(", acquire, StringComparison.Ordinal);

        string queue = ExtractSection(
            source,
            "protected void QueueInteractionGuardedShow(",
            "private sealed class FlyoutInteractionHandoff");
        Assert.Contains("catch (Exception ex)", queue, StringComparison.Ordinal);
        // The queued show is awaited so the successor flyout acquires its own
        // interaction before the handoff is released.
        Assert.True(
            queue.IndexOf("await showAsync();", StringComparison.Ordinal) <
            queue.IndexOf("interactionHandoff?.Dispose();", StringComparison.Ordinal));

        string lease = ExtractSection(
            source,
            "private sealed class FlyoutInteractionHandoff",
            "// ── Tray animation helpers");
        Assert.Contains("owner.EndCompactInteraction();", lease, StringComparison.Ordinal);
        Assert.Contains("widgetManager?.EndWidgetInteraction(", lease, StringComparison.Ordinal);
    }

    [Fact]
    public void ForegroundColorPicker_FlyoutShowFailureRollsBackInteractionLease()
    {
        string source = File.ReadAllText(TestPaths.FromRepository(
            "src/DeskBox/Views/ContentWidgetWindow.Commands.cs"));

        string show = ExtractSection(
            source,
            "private void ShowFlyoutWithInteraction(FlyoutBase flyout",
            "private sealed class CloseWidgetFlyoutHandoff");
        Assert.Contains("flyout.ShowAt(", show, StringComparison.Ordinal);
        Assert.Contains("catch (Exception ex)", show, StringComparison.Ordinal);
        // ShowAt runs before the catch, and a failed show never raises
        // Closed, so the catch must roll back both interaction ends itself.
        Assert.True(
            show.IndexOf("flyout.ShowAt(", StringComparison.Ordinal) <
            show.IndexOf("catch (Exception ex)", StringComparison.Ordinal));

        string rollback = show[show.IndexOf(
            "catch (Exception ex)",
            StringComparison.Ordinal)..];
        Assert.Contains("EndCompactInteraction();", rollback, StringComparison.Ordinal);
        Assert.Contains("EndWidgetInteraction(", rollback, StringComparison.Ordinal);
    }

    private static string ExtractSection(
        string source,
        string startMarker,
        string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing start marker: {startMarker}");
        Assert.True(end > start, $"Missing end marker: {endMarker}");
        return source[start..end];
    }
}
