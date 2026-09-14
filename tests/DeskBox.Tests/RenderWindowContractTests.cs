namespace DeskBox.Tests;

public sealed class RenderWindowContractTests
{
    /// <summary>
    /// Large folders must never hand their full item list to XAML: both item
    /// views bind the windowed prefix, otherwise entering a folder with
    /// thousands of items lays out every tile at once (measured 2026-09-14:
    /// ~50 s of UI-thread layout for 2165 items).
    /// </summary>
    [Fact]
    public void FileSurface_BindsTheRenderWindowInsteadOfTheFullProjection()
    {
        string xaml = File.ReadAllText(GetRepoFile(
            "src/DeskBox/Controls/WidgetContents/FileSurfaceContent.xaml"));

        Assert.DoesNotContain(
            "ItemsSource=\"{Binding VisibleItems}\"",
            xaml,
            StringComparison.Ordinal);
        Assert.Equal(
            2,
            CountOccurrences(xaml, "ItemsSource=\"{Binding RenderedItems}\""));
    }

    [Fact]
    public void AotBindableProperties_RegistersTheRenderWindow()
    {
        string source = File.ReadAllText(GetRepoFile(
            "src/DeskBox/ViewModels/WidgetViewModel.AotBindableProperties.cs"));

        Assert.Contains("nameof(RenderedItems)", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Icons, folder counts, shortcut targets, and shell kinds must all draw
    /// from the windowed hydration universe so metadata work stays proportional
    /// to what the user can see.
    /// </summary>
    [Fact]
    public void ItemHydration_UsesTheRenderWindowUniverse()
    {
        string source = File.ReadAllText(GetRepoFile(
            "src/DeskBox/ViewModels/WidgetViewModel.ItemHydration.cs"));

        Assert.Equal(4, CountOccurrences(source, "HydrationUniverseItems"));
    }

    [Fact]
    public void Windowing_KeepsStackGroupingOnTheFullListAndResetsOnNavigation()
    {
        string windowing = File.ReadAllText(GetRepoFile(
            "src/DeskBox/ViewModels/WidgetViewModel.Windowing.cs"));
        string navigation = File.ReadAllText(GetRepoFile(
            "src/DeskBox/ViewModels/WidgetViewModel.Navigation.cs"));

        // Stack grouping reads ShellKind from every item, so the universe must
        // fall back to the full Items list while stacks are enabled.
        Assert.Contains("UsesStackProjection", windowing, StringComparison.Ordinal);
        // A window grown in one folder must not carry into the next one.
        Assert.Contains("ResetRenderWindow()", navigation, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string GetRepoFile(string relativePath)
    {
        string? directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            string candidate = Path.Combine(directory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new FileNotFoundException($"Could not locate repository file: {relativePath}");
    }
}
