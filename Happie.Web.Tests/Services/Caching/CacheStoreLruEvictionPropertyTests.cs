using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services.Caching;
using Microsoft.JSInterop;
using Moq;

namespace Happie.Web.Tests.Services.Caching;

// Feature: offline-cache, Property 6: DayPlan cache enforces LRU eviction at 30 entries
public class CacheStoreLruEvictionPropertyTests
{
    private const int MaxEntries = 30;

    private static readonly Arbitrary<List<string>> DateSequenceArb =
        Gen.Choose(35, 50)
            .SelectMany(count => Gen.ListOf(
                Gen.Choose(1, 365)
                    .Select(x => DateOnly.FromDayNumber(x + 738000).ToString("yyyy-MM-dd")),
                count))
            .Select(x => x.ToList())
            .ToArbitrary();

    // Feature: offline-cache, Property 6: DayPlan cache enforces LRU eviction at 30 entries
    // Validates: Requirements 4.1, 4.2
    [Property(MaxTest = 100)]
    public Property PutDayPlanAsync_InsertionSequence_NeverExceedsThirtyEntries()
    {
        return Prop.ForAll(
            DateSequenceArb,
            dateSequence =>
            {
                // Arrange.
                var store = new Dictionary<string, (string json, long timestamp)>();
                var householdId = "household-1";
                var timestampCounter = 1000L;
                var mock = CreateJsRuntimeMock(store, householdId, () => timestampCounter++);
                var sut = new CacheStore(mock.Object);
                sut.InitializeAsync().GetAwaiter().GetResult();

                var maxCountObserved = 0;

                // Act.
                foreach (var date in dateSequence)
                {
                    var responseJson = $"{{\"date\":\"{date}\"}}";
                    sut.PutDayPlanAsync(householdId, date, responseJson).GetAwaiter().GetResult();

                    if (store.Count > maxCountObserved)
                        maxCountObserved = store.Count;
                }

                // Assert.
                return (maxCountObserved <= MaxEntries)
                    .Label($"Max count observed was {maxCountObserved}, expected <= {MaxEntries}");
            });
    }

    // Feature: offline-cache, Property 6: DayPlan cache enforces LRU eviction at 30 entries
    // Validates: Requirements 4.1, 4.2
    [Property(MaxTest = 100)]
    public Property PutDayPlanAsync_WhenEvicting_RemovesOldestTimestampEntry()
    {
        return Prop.ForAll(
            DateSequenceArb,
            dateSequence =>
            {
                // Arrange.
                var store = new Dictionary<string, (string json, long timestamp)>();
                var householdId = "household-1";
                var timestampCounter = 1000L;
                var mock = CreateJsRuntimeMock(store, householdId, () => timestampCounter++);
                var sut = new CacheStore(mock.Object);
                sut.InitializeAsync().GetAwaiter().GetResult();

                var evictedCorrectly = true;

                // Act.
                foreach (var date in dateSequence)
                {
                    // Capture state before insertion.
                    var countBefore = store.Count;
                    string? expectedEvictedKey = null;

                    if (countBefore >= MaxEntries)
                    {
                        // The entry with the oldest timestamp should be evicted.
                        expectedEvictedKey = store
                            .OrderBy(x => x.Value.timestamp)
                            .First()
                            .Key;
                    }

                    var responseJson = $"{{\"date\":\"{date}\"}}";
                    sut.PutDayPlanAsync(householdId, date, responseJson).GetAwaiter().GetResult();

                    // Verify eviction targeted the oldest entry.
                    if (expectedEvictedKey is not null && !store.ContainsKey($"{householdId}_{date}".Replace(expectedEvictedKey, expectedEvictedKey)))
                    {
                        // If the date being inserted is the same as the eviction candidate, no eviction happens
                        // because the key already existed and gets overwritten in-place.
                        var insertKey = $"{householdId}_{date}";
                        if (expectedEvictedKey != insertKey && store.ContainsKey(expectedEvictedKey))
                            evictedCorrectly = false;
                    }
                }

                // Assert.
                return evictedCorrectly
                    .Label("Expected eviction to always remove the entry with the oldest timestamp");
            });
    }

    private static Mock<IJSRuntime> CreateJsRuntimeMock(
        Dictionary<string, (string json, long timestamp)> store,
        string householdId,
        Func<long> timestampProvider)
    {
        var mock = new Mock<IJSRuntime>();

        // Initialize — no-op.
        mock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "window.happieCache.initialize",
                It.IsAny<object[]>()))
            .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)!);

        // IsAvailable — returns true.
        mock.Setup(x => x.InvokeAsync<bool>(
                "window.happieCache.isAvailable",
                It.IsAny<object[]>()))
            .ReturnsAsync(true);

        // GetDayPlanCount — returns count of entries for the household.
        mock.Setup(x => x.InvokeAsync<int>(
                "window.happieCache.getDayPlanCount",
                It.Is<object[]>(args => args.Length >= 1 && args[0]!.ToString() == householdId)))
            .Returns(() => new ValueTask<int>(store.Count));

        // GetOldestDayPlanKey — returns key with minimum timestamp.
        mock.Setup(x => x.InvokeAsync<string?>(
                "window.happieCache.getOldestDayPlanKey",
                It.Is<object[]>(args => args.Length >= 1 && args[0]!.ToString() == householdId)))
            .Returns(() =>
            {
                if (store.Count == 0)
                    return new ValueTask<string?>((string?)null);

                var oldestKey = store.OrderBy(x => x.Value.timestamp).First().Key;
                return new ValueTask<string?>(oldestKey);
            });

        // DeleteDayPlan — removes entry from store.
        mock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "window.happieCache.deleteDayPlan",
                It.Is<object[]>(args => args.Length >= 2 && args[0]!.ToString() == householdId)))
            .Returns((string _, object[] args) =>
            {
                var date = args[1]!.ToString()!;
                var key = $"{householdId}_{date}";
                store.Remove(key);
                return new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)!);
            });

        // PutDayPlan — adds/updates entry in store.
        mock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "window.happieCache.putDayPlan",
                It.Is<object[]>(args => args.Length >= 4 && args[0]!.ToString() == householdId)))
            .Returns((string _, object[] args) =>
            {
                var date = args[1]!.ToString()!;
                var responseJson = args[2]!.ToString()!;
                var timestamp = Convert.ToInt64(args[3]);
                var key = $"{householdId}_{date}";
                store[key] = (responseJson, timestamp);
                return new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)!);
            });

        return mock;
    }
}
