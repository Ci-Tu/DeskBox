using System.Text.Json;
using DeskBox.Models;
using DeskBox.Services;
using Xunit.Abstractions;

namespace DeskBox.Tests;

public sealed class OrganizationHistoryPolicyTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempRoot;

    public OrganizationHistoryPolicyTests(ITestOutputHelper output)
    {
        _output = output;
        _tempRoot = Path.Combine(Path.GetTempPath(), "DeskBox.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private static OrganizationHistoryEntry CreateEntry(
        int itemCount,
        bool canUndo = true,
        int pathLength = 40,
        DateTime? timestampUtc = null,
        string? errorMessage = null)
    {
        string filler = new('a', Math.Max(0, pathLength - 20));
        return new OrganizationHistoryEntry
        {
            Id = Guid.NewGuid().ToString(),
            TimestampUtc = timestampUtc ?? DateTime.UtcNow,
            WidgetId = "widget",
            WidgetName = "Widget",
            CanUndo = canUndo,
            ErrorMessage = errorMessage,
            Items = Enumerable.Range(0, itemCount).Select(i => new OrganizationHistoryItem
            {
                Name = $"{filler}-{i}.txt",
                SourcePath = $"C:\\src\\{filler}-{i}.txt",
                DestinationPath = $"E:\\dst\\{filler}-{i}.txt",
                TargetWidgetId = "widget",
                TargetWidgetName = "Widget"
            }).ToList()
        };
    }

    [Fact]
    public void PerEntryCap_KeepsReceiptsAtLimit()
    {
        foreach (int count in (int[])[499, 500])
        {
            var history = new List<OrganizationHistoryEntry> { CreateEntry(count, canUndo: true) };

            bool changed = OrganizationHistoryPolicy.ApplyRetentionPolicy(history);

            var entry = Assert.Single(history);
            Assert.True(changed); // TotalItemCount backfill counts as a change.
            Assert.True(entry.CanUndo);
            Assert.Equal(count, entry.Items.Count);
            Assert.Equal(count, entry.TotalItemCount);
            Assert.Equal(count, entry.ItemCount);
        }
    }

    [Fact]
    public void PerEntryCap_DowngradesAboveLimit()
    {
        var entry = CreateEntry(501, canUndo: true);
        entry.UndoStarted = true;
        var history = new List<OrganizationHistoryEntry> { entry };

        bool changed = OrganizationHistoryPolicy.ApplyRetentionPolicy(history);
        bool changedAgain = OrganizationHistoryPolicy.ApplyRetentionPolicy(history);

        var result = Assert.Single(history);
        Assert.True(changed);
        Assert.False(changedAgain); // Idempotent.
        Assert.False(result.CanUndo);
        Assert.False(result.UndoStarted);
        Assert.Empty(result.Items);
        Assert.Equal(501, result.TotalItemCount);
        Assert.Equal(501, result.ItemCount);
    }

    [Fact]
    public void GlobalBudget_DowngradesOldestFirst()
    {
        // 6 entries x 500 receipts = 3000 > 2500 budget. The list is
        // newest-first, so the oldest entry (last index) loses its receipts
        // first and the five newest stay fully undoable (= 2500 exactly).
        var history = Enumerable.Range(0, 6)
            .Select(i => CreateEntry(500, timestampUtc: DateTime.UtcNow.AddMinutes(i)))
            .ToList();

        OrganizationHistoryPolicy.ApplyRetentionPolicy(history);

        Assert.Equal(6, history.Count);
        Assert.All(history.Take(5), entry =>
        {
            Assert.True(entry.CanUndo);
            Assert.Equal(500, entry.Items.Count);
        });
        var downgraded = history[^1];
        Assert.False(downgraded.CanUndo);
        Assert.Empty(downgraded.Items);
        Assert.Equal(500, downgraded.TotalItemCount);
        Assert.Equal(2500, history.Sum(entry => entry.Items.Count));
    }

    [Fact]
    public void GlobalBudget_KeepsNormalSizedHistory()
    {
        var history = Enumerable.Range(0, SettingsService.MaxRecentOrganizationHistoryCount)
            .Select(i => CreateEntry(20, timestampUtc: DateTime.UtcNow.AddMinutes(i)))
            .ToList();

        OrganizationHistoryPolicy.ApplyRetentionPolicy(history);

        Assert.Equal(SettingsService.MaxRecentOrganizationHistoryCount, history.Count);
        Assert.All(history, entry =>
        {
            Assert.True(entry.CanUndo);
            Assert.Equal(20, entry.Items.Count);
        });
    }

    [Fact]
    public void LegacyEntry_ItemCountFallsBackToItems()
    {
        var legacy = new OrganizationHistoryEntry { Items = CreateItems(5) };
        Assert.Equal(0, legacy.TotalItemCount);
        Assert.Equal(5, legacy.ItemCount);

        var summary = new OrganizationHistoryEntry { TotalItemCount = 2088 };
        Assert.Equal(2088, summary.ItemCount);
    }

    [Fact]
    public void LongPaths_DoNotTriggerDowngradeWithinCountCap()
    {
        // The cap is deliberately count-based: measuring bytes would need a
        // serialization pass on every append. 500 receipts with very long
        // paths stay retained.
        var history = new List<OrganizationHistoryEntry> { CreateEntry(500, pathLength: 1000) };

        OrganizationHistoryPolicy.ApplyRetentionPolicy(history);

        var entry = Assert.Single(history);
        Assert.True(entry.CanUndo);
        Assert.Equal(500, entry.Items.Count);
        Assert.All(entry.Items, item => Assert.True(item.SourcePath.Length >= 900));
    }

    [Fact]
    public async Task DowngradedEntry_IsNeverUndoable()
    {
        string desktopRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, "desktop")).FullName;
        var settings = new SettingsService(Path.Combine(_tempRoot, "settings"));
        var entry = CreateEntry(600);
        settings.Settings.RecentOrganizationHistory.Add(entry);
        OrganizationHistoryPolicy.ApplyRetentionPolicy(settings.Settings.RecentOrganizationHistory);

        var organizer = new OrganizerService(settings, new FileService(), () => desktopRoot);

        Assert.Null(organizer.GetLatestUndoableEntry());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => organizer.UndoAsync(entry.Id));
    }

    [Fact]
    public async Task LoadAsync_MigratesBloatShrinksFileAndKeepsEntryCap()
    {
        string dataDir = Path.Combine(_tempRoot, "settings");
        Directory.CreateDirectory(dataDir);
        var bloated = new AppSettings();
        bloated.RecentOrganizationHistory.AddRange(Enumerable.Range(0, 30).Select(i =>
            CreateEntry(2000, timestampUtc: DateTime.UtcNow.AddMinutes(i))));
        string settingsPath = Path.Combine(dataDir, "settings.json");
        await File.WriteAllTextAsync(
            settingsPath,
            JsonSerializer.Serialize(bloated, SettingsJsonContext.Default.AppSettings));
        long originalLength = new FileInfo(settingsPath).Length;

        var service = new SettingsService(dataDir);
        await service.LoadAsync();

        long migratedLength = new FileInfo(settingsPath).Length;
        _output.WriteLine($"settings.json: {originalLength} -> {migratedLength} bytes");
        Assert.Equal(SettingsService.MaxRecentOrganizationHistoryCount, service.Settings.RecentOrganizationHistory.Count);
        Assert.True(migratedLength < originalLength / 10, $"expected a >10x shrink, got {originalLength} -> {migratedLength}");
        Assert.All(service.Settings.RecentOrganizationHistory, entry =>
        {
            Assert.False(entry.CanUndo);
            Assert.Empty(entry.Items);
            Assert.Equal(2000, entry.TotalItemCount);
        });
    }

    [Fact]
    public async Task LoadAsync_PreservesSmallUndoableHistory()
    {
        string dataDir = Path.Combine(_tempRoot, "settings");
        Directory.CreateDirectory(dataDir);
        var profile = new AppSettings();
        profile.RecentOrganizationHistory.AddRange(Enumerable.Range(0, 3).Select(i =>
            CreateEntry(20, timestampUtc: DateTime.UtcNow.AddMinutes(i))));
        await File.WriteAllTextAsync(
            Path.Combine(dataDir, "settings.json"),
            JsonSerializer.Serialize(profile, SettingsJsonContext.Default.AppSettings));

        var service = new SettingsService(dataDir);
        await service.LoadAsync();

        Assert.Equal(3, service.Settings.RecentOrganizationHistory.Count);
        Assert.All(service.Settings.RecentOrganizationHistory, entry =>
        {
            Assert.True(entry.CanUndo);
            Assert.Equal(20, entry.Items.Count);
        });
    }

    /// <summary>
    /// Local-only harness: runs the load migration over a real (uncommitted)
    /// settings.json copy pointed to by DESKBOX_HISTORY_MIGRATION_FIXTURE.
    /// Skips silently when the variable is unset, e.g. in CI.
    /// </summary>
    [Fact]
    public async Task RealSettingsCopy_MigrationShrinksBloat()
    {
        string? fixturePath = Environment.GetEnvironmentVariable("DESKBOX_HISTORY_MIGRATION_FIXTURE");
        if (string.IsNullOrWhiteSpace(fixturePath) || !File.Exists(fixturePath))
        {
            return;
        }

        string json = await File.ReadAllTextAsync(fixturePath);
        long originalLength = new FileInfo(fixturePath).Length;
        var settings = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);
        Assert.NotNull(settings);

        OrganizationHistoryPolicy.ApplyRetentionPolicy(settings.RecentOrganizationHistory);

        string migrated = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings);
        _output.WriteLine(
            $"fixture: {originalLength} -> {migrated.Length} bytes, " +
            $"entries={settings.RecentOrganizationHistory.Count}, " +
            $"retainedReceipts={settings.RecentOrganizationHistory.Sum(entry => entry.Items.Count)}");
        Assert.True(migrated.Length < originalLength / 5, $"expected a >5x shrink, got {originalLength} -> {migrated.Length}");
        Assert.All(settings.RecentOrganizationHistory, entry =>
        {
            Assert.True(entry.Items.Count <= OrganizationHistoryPolicy.MaxUndoReceiptItemsPerEntry);
            // Receipts are all-or-nothing: no entry may stay undoable once
            // its receipt list is empty.
            if (entry.Items.Count == 0)
            {
                Assert.False(entry.CanUndo);
            }
        });
    }

    private static List<OrganizationHistoryItem> CreateItems(int count) => Enumerable.Range(0, count)
        .Select(i => new OrganizationHistoryItem
        {
            Name = $"file-{i}.txt",
            SourcePath = $"C:\\src\\file-{i}.txt",
            DestinationPath = $"E:\\dst\\file-{i}.txt"
        })
        .ToList();
}
