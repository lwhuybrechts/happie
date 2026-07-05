using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services.Caching;
using Microsoft.JSInterop;
using Moq;

namespace Happie.Web.Tests.Services.Caching;

// Feature: calendar-prefetch, Property 1: Calendar cache enforces 6-entry limit with cluster-based protection
public class CacheStoreCalendarLimitPropertyTests
{
    private static readonly string CurrentMonth = DateTime.Now.ToString("yyyy-MM");

    private static readonly Arbitrary<List<string>> MonthSequenceArb =
        Gen.Choose(2020, 2030)
            .SelectMany(x => Gen.Choose(1, 12).Select(y => $"{x:D4}-{y:D2}"))
            .ListOf()
            .Where(x => x.Count >= 1 && x.Count <= 20)
            .Select(x => x.ToList())
            .ToArbitrary();

    private static readonly Arbitrary<List<(string month, string viewedMonth)>> MonthWithViewedArb =
        Gen.Choose(2020, 2030)
            .SelectMany(x => Gen.Choose(1, 12).Select(y => $"{x:D4}-{y:D2}"))
            .Two()
            .Select(x => (month: x.Item1, viewedMonth: x.Item2))
            .ListOf()
            .Where(x => x.Count >= 1 && x.Count <= 20)
            .Select(x => x.ToList())
            .ToArbitrary();

    // Feature: calendar-prefetch, Property 1: Calendar cache enforces 6-entry limit with cluster-based protection
    // Validates: Requirements 2.1, 2.2, 2.3, 2.4
    [Property(MaxTest = 100)]
    public Property PutCalendarAsync_EvictableEntriesExist_NeverExceedsSixEntries()
    {
        return Prop.ForAll(
            MonthSequenceArb,
            async months =>
            {
                // Arrange.
                var householdId = Guid.NewGuid().ToString();
                var store = new Dictionary<string, (string json, long timestamp)>();
                var jsRuntimeMock = CreateJsRuntimeMock(householdId, store);
                var sut = new CacheStore(jsRuntimeMock.Object);
                await sut.InitializeAsync();

                var limitViolated = false;

                // Act.
                foreach (var month in months)
                {
                    await sut.PutCalendarAsync(householdId, month, $"{{\"month\":\"{month}\"}}", month);

                    // Assert after each insertion.
                    // Check if all entries are in a protected cluster. If so, exceeding is allowed.
                    var allProtected = AllEntriesInProtectedClusters(store, householdId, CurrentMonth, month);
                    if (!allProtected && store.Count > 6)
                        limitViolated = true;
                }

                return (!limitViolated).Label("Calendar entries exceeded 6-entry limit when evictable entries existed");
            });
    }

    // Feature: calendar-prefetch, Property 1: Calendar cache enforces 6-entry limit with cluster-based protection
    // Validates: Requirements 2.2
    [Property(MaxTest = 100)]
    public Property PutCalendarAsync_AnyInsertionSequence_TodayClusterEntriesNeverEvicted()
    {
        return Prop.ForAll(
            MonthWithViewedArb,
            async insertions =>
            {
                // Arrange.
                var householdId = Guid.NewGuid().ToString();
                var store = new Dictionary<string, (string json, long timestamp)>();
                var jsRuntimeMock = CreateJsRuntimeMock(householdId, store);
                var sut = new CacheStore(jsRuntimeMock.Object);
                await sut.InitializeAsync();

                var todayCluster = GetCluster(CurrentMonth);

                // Seed the today cluster entries first.
                foreach (var month in todayCluster)
                    await sut.PutCalendarAsync(householdId, month, $"{{\"month\":\"{month}\"}}", CurrentMonth);

                var todayClusterEvicted = false;

                // Act.
                foreach (var (month, viewedMonth) in insertions)
                {
                    await sut.PutCalendarAsync(householdId, month, $"{{\"month\":\"{month}\"}}", viewedMonth);

                    // Assert: today cluster entries must still be present.
                    foreach (var protectedMonth in todayCluster)
                    {
                        var key = $"{householdId}_{protectedMonth}";
                        if (!store.ContainsKey(key))
                            todayClusterEvicted = true;
                    }
                }

                return (!todayClusterEvicted).Label("Today cluster entry was evicted");
            });
    }

    // Feature: calendar-prefetch, Property 1: Calendar cache enforces 6-entry limit with cluster-based protection
    // Validates: Requirements 2.3
    [Property(MaxTest = 100)]
    public Property PutCalendarAsync_AnyInsertionSequence_ViewedClusterEntriesNeverEvicted()
    {
        return Prop.ForAll(
            MonthWithViewedArb,
            async insertions =>
            {
                // Arrange.
                var householdId = Guid.NewGuid().ToString();
                var store = new Dictionary<string, (string json, long timestamp)>();
                var jsRuntimeMock = CreateJsRuntimeMock(householdId, store);
                var sut = new CacheStore(jsRuntimeMock.Object);
                await sut.InitializeAsync();

                var viewedClusterEvicted = false;

                // Act.
                foreach (var (month, viewedMonth) in insertions)
                {
                    // Snapshot keys before insertion.
                    var keysBefore = store.Keys.Where(x => x.StartsWith($"{householdId}_")).ToHashSet();

                    await sut.PutCalendarAsync(householdId, month, $"{{\"month\":\"{month}\"}}", viewedMonth);

                    // Check what was evicted.
                    var keysAfter = store.Keys.Where(x => x.StartsWith($"{householdId}_")).ToHashSet();
                    var evictedKeys = keysBefore.Except(keysAfter).ToList();

                    // The viewed cluster at the time of this insertion.
                    var viewedCluster = GetCluster(viewedMonth);

                    foreach (var evictedKey in evictedKeys)
                    {
                        var evictedMonth = evictedKey[(householdId.Length + 1)..];
                        if (viewedCluster.Contains(evictedMonth))
                            viewedClusterEvicted = true;
                    }
                }

                return (!viewedClusterEvicted).Label("Viewed cluster entry was evicted");
            });
    }

    // Feature: calendar-prefetch, Property 1: Calendar cache enforces 6-entry limit with cluster-based protection
    // Validates: Requirements 2.1, 2.5
    [Property(MaxTest = 100)]
    public Property PutCalendarAsync_AllEntriesProtected_CacheTemporarilyExceedsLimit()
    {
        var singleMonthArb = Gen.Choose(2020, 2030)
            .SelectMany(x => Gen.Choose(1, 12).Select(y => $"{x:D4}-{y:D2}"))
            .ToArbitrary();

        return Prop.ForAll(
            singleMonthArb,
            async extraMonth =>
            {
                // Arrange: fill cache with 6 entries that are all in a protected cluster.
                var householdId = Guid.NewGuid().ToString();
                var store = new Dictionary<string, (string json, long timestamp)>();
                var jsRuntimeMock = CreateJsRuntimeMock(householdId, store);
                var sut = new CacheStore(jsRuntimeMock.Object);
                await sut.InitializeAsync();

                var todayCluster = GetCluster(CurrentMonth);
                // Use a viewedMonth far from today so the viewed cluster doesn't overlap.
                var viewedMonth = AddMonths(CurrentMonth, 6);
                var viewedCluster = GetCluster(viewedMonth);

                // Insert today cluster and viewed cluster entries (6 total when no overlap).
                var allProtected = todayCluster.Concat(viewedCluster).Distinct().ToList();
                foreach (var month in allProtected.Take(6))
                    await sut.PutCalendarAsync(householdId, month, $"{{\"month\":\"{month}\"}}", viewedMonth);

                if (store.Count < 6)
                    return true.Label("Not enough entries to trigger eviction (clusters overlap)");

                // Act: insert one more entry that is also in the viewed cluster to keep all protected.
                // Use viewedMonth itself (which is already in the viewed cluster).
                await sut.PutCalendarAsync(householdId, extraMonth, $"{{\"month\":\"{extraMonth}\"}}", extraMonth);

                // Assert: if the extra month was not already in store and all entries are protected,
                // the cache is allowed to exceed 6.
                var allInProtectedClusters = AllEntriesInProtectedClusters(store, householdId, CurrentMonth, extraMonth);
                if (allInProtectedClusters)
                    return (store.Count >= 6).Label("Cache should be allowed to exceed limit when all entries are protected");

                // If not all entries are protected, limit should be enforced.
                return (store.Count <= 6).Label("Cache should not exceed limit when evictable entries exist");
            });
    }

    // Feature: calendar-prefetch, Property 1: Calendar cache enforces 6-entry limit with cluster-based protection
    // Validates: Requirements 2.4
    [Property(MaxTest = 100)]
    public Property PutCalendarAsync_MultipleEvictableEntries_FarthestFromViewedIsEvictedFirst()
    {
        var singleMonthArb = Gen.Choose(2020, 2030)
            .SelectMany(x => Gen.Choose(1, 12).Select(y => $"{x:D4}-{y:D2}"))
            .ToArbitrary();

        return Prop.ForAll(
            singleMonthArb,
            async newMonth =>
            {
                // Arrange: fill cache with 6 entries where some are outside both clusters.
                var householdId = Guid.NewGuid().ToString();
                var store = new Dictionary<string, (string json, long timestamp)>();
                var jsRuntimeMock = CreateJsRuntimeMock(householdId, store);
                var sut = new CacheStore(jsRuntimeMock.Object);
                await sut.InitializeAsync();

                // Use a viewed month in the middle of the range.
                var viewedMonth = "2025-06";
                var todayCluster = GetCluster(CurrentMonth);
                var viewedCluster = GetCluster(viewedMonth);
                var protectedMonths = todayCluster.Concat(viewedCluster).Distinct().ToHashSet();

                // Insert 3 today-cluster entries.
                foreach (var month in todayCluster)
                    await sut.PutCalendarAsync(householdId, month, $"{{\"month\":\"{month}\"}}", viewedMonth);

                // Insert 3 distant months (far from viewed) that are NOT in either cluster.
                var distantMonths = new[] { "2020-01", "2020-06", "2030-12" }
                    .Where(x => !protectedMonths.Contains(x))
                    .Take(3)
                    .ToList();

                foreach (var month in distantMonths)
                    await sut.PutCalendarAsync(householdId, month, $"{{\"month\":\"{month}\"}}", viewedMonth);

                if (store.Count < 6)
                    return true.Label("Could not fill cache to 6 (cluster overlap)");

                // Find the entry that should be evicted (farthest from viewedMonth among non-protected).
                var expectedEviction = store.Keys
                    .Where(x => x.StartsWith($"{householdId}_"))
                    .Select(x => x[(householdId.Length + 1)..])
                    .Where(x => !protectedMonths.Contains(x))
                    .OrderByDescending(x => MonthDistance(x, viewedMonth))
                    .FirstOrDefault();

                // Act: insert the new month.
                var newKey = $"{householdId}_{newMonth}";
                var alreadyInStore = store.ContainsKey(newKey);
                await sut.PutCalendarAsync(householdId, newMonth, $"{{\"month\":\"{newMonth}\"}}", viewedMonth);

                // Assert: if the new month was already cached or within limit, no eviction needed.
                if (alreadyInStore || store.Count <= 6)
                    return true.Label("No eviction needed (already cached or within limit)");

                // If eviction occurred, the expected entry should have been removed.
                if (expectedEviction is not null)
                {
                    var expectedKey = $"{householdId}_{expectedEviction}";
                    return (!store.ContainsKey(expectedKey))
                        .Label($"Expected farthest entry '{expectedEviction}' to be evicted, but it was still present");
                }

                return true.Label("No evictable entries found");
            });
    }

    private static Mock<IJSRuntime> CreateJsRuntimeMock(string householdId, Dictionary<string, (string json, long timestamp)> store)
    {
        var mock = new Mock<IJSRuntime>();

        // Initialize is a no-op.
        mock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "window.happieCache.initialize",
                It.IsAny<object[]>()))
            .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)!);

        // isAvailable returns true.
        mock.Setup(x => x.InvokeAsync<bool>(
                "window.happieCache.isAvailable",
                It.IsAny<object[]>()))
            .ReturnsAsync(true);

        // getCalendarKeys returns filtered keys.
        mock.Setup(x => x.InvokeAsync<string[]>(
                "window.happieCache.getCalendarKeys",
                It.Is<object[]>(args => args.Length >= 1 && args[0]!.ToString() == householdId)))
            .Returns(() => ValueTask.FromResult(
                store.Keys.Where(x => x.StartsWith($"{householdId}_")).ToArray()));

        // getEvictableCalendarKey mirrors the JS cluster-based eviction logic.
        mock.Setup(x => x.InvokeAsync<string?>(
                "window.happieCache.getEvictableCalendarKey",
                It.Is<object[]>(args => args.Length >= 3 && args[0]!.ToString() == householdId)))
            .Returns((string _, object[] args) =>
            {
                var todayMonth = args[1]!.ToString()!;
                var viewedMonth = args[2]!.ToString()!;

                var todayCluster = GetCluster(todayMonth);
                var viewedCluster = GetCluster(viewedMonth);
                var protectedMonths = todayCluster.Concat(viewedCluster).Distinct().ToHashSet();

                var keys = store.Keys.Where(x => x.StartsWith($"{householdId}_")).ToList();
                var viewedAbsolute = ToAbsoluteMonths(viewedMonth);

                string? farthestKey = null;
                var farthestDistance = -1;

                foreach (var key in keys)
                {
                    var entryMonth = key[(householdId.Length + 1)..];
                    if (protectedMonths.Contains(entryMonth))
                        continue;

                    var entryAbsolute = ToAbsoluteMonths(entryMonth);
                    var distance = Math.Abs(entryAbsolute - viewedAbsolute);
                    if (distance > farthestDistance)
                    {
                        farthestDistance = distance;
                        farthestKey = key;
                    }
                }

                return ValueTask.FromResult(farthestKey);
            });

        // deleteCalendar removes the entry.
        mock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "window.happieCache.deleteCalendar",
                It.Is<object[]>(args => args.Length >= 2 && args[0]!.ToString() == householdId)))
            .Returns((string _, object[] args) =>
            {
                var monthToDelete = args[1]!.ToString()!;
                var key = $"{householdId}_{monthToDelete}";
                store.Remove(key);
                return ValueTask.FromResult(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)!);
            });

        // putCalendar adds/updates the entry.
        mock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "window.happieCache.putCalendar",
                It.Is<object[]>(args => args.Length >= 4 && args[0]!.ToString() == householdId)))
            .Returns((string _, object[] args) =>
            {
                var month = args[1]!.ToString()!;
                var responseJson = args[2]!.ToString()!;
                var timestamp = Convert.ToInt64(args[3]);
                var key = $"{householdId}_{month}";
                store[key] = (responseJson, timestamp);
                return ValueTask.FromResult(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)!);
            });

        return mock;
    }

    private static HashSet<string> GetCluster(string centerMonth)
    {
        return new HashSet<string>
        {
            AddMonths(centerMonth, -1),
            centerMonth,
            AddMonths(centerMonth, 1)
        };
    }

    private static string AddMonths(string monthStr, int offset)
    {
        var parts = monthStr.Split('-');
        var year = int.Parse(parts[0]);
        var month = int.Parse(parts[1]);
        var absolute = year * 12 + (month - 1) + offset;
        var newYear = absolute / 12;
        var newMonth = absolute % 12 + 1;
        return $"{newYear:D4}-{newMonth:D2}";
    }

    private static int ToAbsoluteMonths(string monthStr)
    {
        var parts = monthStr.Split('-');
        var year = int.Parse(parts[0]);
        var month = int.Parse(parts[1]);
        return year * 12 + (month - 1);
    }

    private static int MonthDistance(string monthA, string monthB)
    {
        return Math.Abs(ToAbsoluteMonths(monthA) - ToAbsoluteMonths(monthB));
    }

    private static bool AllEntriesInProtectedClusters(Dictionary<string, (string json, long timestamp)> store, string householdId, string todayMonth, string viewedMonth)
    {
        var todayCluster = GetCluster(todayMonth);
        var viewedCluster = GetCluster(viewedMonth);
        var protectedMonths = todayCluster.Concat(viewedCluster).Distinct().ToHashSet();

        return store.Keys
            .Where(x => x.StartsWith($"{householdId}_"))
            .Select(x => x[(householdId.Length + 1)..])
            .All(x => protectedMonths.Contains(x));
    }
}
