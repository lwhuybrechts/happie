using System.Text.Json;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services.Caching;
using Microsoft.JSInterop;
using Moq;

namespace Happie.Web.Tests.Services.Caching;

// Feature: offline-cache, Property 17: Cache and queue isolated by household
public class CacheStoreHouseholdIsolationPropertyTests
{
    private static readonly Arbitrary<(string HouseholdA, string HouseholdB)> DistinctHouseholdPairArb =
        Gen.Fresh(() => Guid.NewGuid().ToString())
            .SelectMany(a => Gen.Fresh(() => Guid.NewGuid().ToString())
                .Where(b => b != a)
                .Select(b => (HouseholdA: a, HouseholdB: b)))
            .ToArbitrary();

    private static readonly Arbitrary<string> DateArb =
        Gen.Choose(2020, 2030)
            .SelectMany(year => Gen.Choose(1, 12)
                .SelectMany(month => Gen.Choose(1, DateTime.DaysInMonth(year, month))
                    .Select(day => $"{year:D4}-{month:D2}-{day:D2}")))
            .ToArbitrary();

    private static readonly Arbitrary<string> MonthArb =
        Gen.Choose(2020, 2030)
            .SelectMany(year => Gen.Choose(1, 12)
                .Select(month => $"{year:D4}-{month:D2}"))
            .ToArbitrary();

    private static readonly Arbitrary<string> JsonArb =
        Gen.Elements(
                "{\"attendance\":[{\"id\":\"abc\",\"status\":\"EatingIn\"}]}",
                "{\"dish\":\"Pasta Carbonara\",\"comments\":[]}",
                "{\"days\":[{\"date\":\"2024-01-15\",\"eatingIn\":[\"#FF0000\"]}]}",
                "{\"empty\":true}",
                "{\"nested\":{\"a\":{\"b\":{\"c\":1}}}}")
            .ToArbitrary();

    private static readonly Arbitrary<QueuedMutation> MutationArb =
        Gen.Choose(1, 1000)
            .SelectMany(id => Gen.Elements("PUT", "DELETE")
                .SelectMany(method => Gen.Elements("/api/days/2024-01-15/attendance/abc", "/api/days/2024-01-15/dish")
                    .SelectMany(url => Gen.Elements("attendance", "dish", "comment")
                        .Select(mutationType => new QueuedMutation(
                            id,
                            string.Empty,
                            method,
                            url,
                            new Dictionary<string, string> { ["Authorization"] = "Bearer test" },
                            method == "PUT" ? "{\"status\":\"EatingIn\"}" : null,
                            DateTimeOffset.UtcNow,
                            DateOnly.FromDateTime(DateTime.Today),
                            mutationType)))))
            .ToArbitrary();

    // Feature: offline-cache, Property 17: Cache and queue isolated by household
    // Validates: Requirements 9.1, 9.3
    [Property(MaxTest = 100)]
    public Property GetDayPlanAsync_StoredUnderHouseholdA_ReturnsNullForHouseholdB()
    {
        return Prop.ForAll(
            DistinctHouseholdPairArb,
            DateArb,
            JsonArb,
            (households, date, responseJson) =>
            {
                // Arrange.
                var dayPlanStore = new Dictionary<string, DayPlanEntry>();
                var mockJsRuntime = CreateMockJsRuntimeForDayPlan(dayPlanStore);
                var sut = CreateInitializedCacheStore(mockJsRuntime.Object);

                // Act.
                sut.PutDayPlanAsync(households.HouseholdA, date, responseJson).GetAwaiter().GetResult();
                var result = sut.GetDayPlanAsync(households.HouseholdB, date).GetAwaiter().GetResult();

                // Assert.
                return (result is null)
                    .Label($"Expected null when reading household B's DayPlan, but got: {result?.ResponseJson ?? "null"}");
            });
    }

    // Feature: offline-cache, Property 17: Cache and queue isolated by household
    // Validates: Requirements 9.1, 9.3
    [Property(MaxTest = 100)]
    public Property GetCalendarAsync_StoredUnderHouseholdA_ReturnsNullForHouseholdB()
    {
        return Prop.ForAll(
            DistinctHouseholdPairArb,
            MonthArb,
            JsonArb,
            (households, month, responseJson) =>
            {
                // Arrange.
                var calendarStore = new Dictionary<string, CalendarEntry>();
                var mockJsRuntime = CreateMockJsRuntimeForCalendar(calendarStore);
                var sut = CreateInitializedCacheStore(mockJsRuntime.Object);

                // Act.
                sut.PutCalendarAsync(households.HouseholdA, month, responseJson, month).GetAwaiter().GetResult();
                var result = sut.GetCalendarAsync(households.HouseholdB, month).GetAwaiter().GetResult();

                // Assert.
                return (result is null)
                    .Label($"Expected null when reading household B's Calendar, but got: {result?.ResponseJson ?? "null"}");
            });
    }

    // Feature: offline-cache, Property 17: Cache and queue isolated by household
    // Validates: Requirements 9.1, 9.3
    [Property(MaxTest = 100)]
    public Property PeekAllAsync_MutationsEnqueuedUnderHouseholdA_ReturnsEmptyForHouseholdB()
    {
        return Prop.ForAll(
            DistinctHouseholdPairArb,
            MutationArb,
            (households, mutation) =>
            {
                // Arrange.
                var mutationStore = new Dictionary<string, Queue<JsonElement>>();
                var mockJsRuntime = CreateMockJsRuntimeForMutationQueue(mutationStore);
                var sut = CreateInitializedMutationQueue(mockJsRuntime.Object);

                var scopedMutation = mutation with { HouseholdId = households.HouseholdA };

                // Act.
                sut.EnqueueAsync(households.HouseholdA, scopedMutation).GetAwaiter().GetResult();
                var result = sut.PeekAllAsync(households.HouseholdB).GetAwaiter().GetResult();

                // Assert.
                return (result.Count == 0)
                    .Label($"Expected empty list for household B, but got {result.Count} mutations");
            });
    }

    // Feature: offline-cache, Property 17: Cache and queue isolated by household
    // Validates: Requirements 9.1, 9.3
    [Property(MaxTest = 100)]
    public Property DequeueAsync_MutationsEnqueuedUnderHouseholdA_ReturnsNullForHouseholdB()
    {
        return Prop.ForAll(
            DistinctHouseholdPairArb,
            MutationArb,
            (households, mutation) =>
            {
                // Arrange.
                var mutationStore = new Dictionary<string, Queue<JsonElement>>();
                var mockJsRuntime = CreateMockJsRuntimeForMutationQueue(mutationStore);
                var sut = CreateInitializedMutationQueue(mockJsRuntime.Object);

                var scopedMutation = mutation with { HouseholdId = households.HouseholdA };

                // Act.
                sut.EnqueueAsync(households.HouseholdA, scopedMutation).GetAwaiter().GetResult();
                var result = sut.DequeueAsync(households.HouseholdB).GetAwaiter().GetResult();

                // Assert.
                return (result is null)
                    .Label("Expected null when dequeuing from household B, but got a mutation");
            });
    }

    private static CacheStore CreateInitializedCacheStore(IJSRuntime jsRuntime)
    {
        var cacheStore = new CacheStore(jsRuntime);
        cacheStore.InitializeAsync().GetAwaiter().GetResult();
        return cacheStore;
    }

    private static MutationQueue CreateInitializedMutationQueue(IJSRuntime jsRuntime)
    {
        var mutationQueue = new MutationQueue(jsRuntime);
        mutationQueue.InitializeAsync().GetAwaiter().GetResult();
        return mutationQueue;
    }

    private static Mock<IJSRuntime> CreateMockJsRuntimeForDayPlan(Dictionary<string, DayPlanEntry> store)
    {
        var mock = new Mock<IJSRuntime>();

        // Initialize — no-op.
        mock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "window.happieCache.initialize", It.IsAny<object[]>()))
            .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)!);

        // IsAvailable — returns true.
        mock.Setup(x => x.InvokeAsync<bool>("window.happieCache.isAvailable", It.IsAny<object[]>()))
            .ReturnsAsync(true);

        // GetDayPlanCount — return count for the given household.
        mock.Setup(x => x.InvokeAsync<int>("window.happieCache.getDayPlanCount", It.IsAny<object[]>()))
            .Returns<string, object[]>((_, args) =>
            {
                var householdId = args[0]?.ToString() ?? string.Empty;
                var count = store.Count(x => x.Key.StartsWith($"{householdId}_"));
                return new ValueTask<int>(count);
            });

        // PutDayPlan — store the entry keyed by {householdId}_{date}.
        mock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "window.happieCache.putDayPlan", It.IsAny<object[]>()))
            .Callback<string, object[]>((_, args) =>
            {
                var householdId = args[0]?.ToString() ?? string.Empty;
                var date = args[1]?.ToString() ?? string.Empty;
                var json = args[2]?.ToString() ?? string.Empty;
                var timestamp = Convert.ToInt64(args[3]);
                var key = $"{householdId}_{date}";
                store[key] = new DayPlanEntry { Date = date, ResponseJson = json, Timestamp = timestamp };
            })
            .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)!);

        // GetDayPlan — retrieve the entry scoped by household.
        mock.Setup(x => x.InvokeAsync<DayPlanEntry?>("window.happieCache.getDayPlan", It.IsAny<object[]>()))
            .Returns<string, object[]>((_, args) =>
            {
                var householdId = args[0]?.ToString() ?? string.Empty;
                var date = args[1]?.ToString() ?? string.Empty;
                var key = $"{householdId}_{date}";
                store.TryGetValue(key, out var entry);
                return ValueTask.FromResult(entry);
            });

        return mock;
    }

    private static Mock<IJSRuntime> CreateMockJsRuntimeForCalendar(Dictionary<string, CalendarEntry> store)
    {
        var mock = new Mock<IJSRuntime>();

        // Initialize — no-op.
        mock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "window.happieCache.initialize", It.IsAny<object[]>()))
            .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)!);

        // IsAvailable — returns true.
        mock.Setup(x => x.InvokeAsync<bool>("window.happieCache.isAvailable", It.IsAny<object[]>()))
            .ReturnsAsync(true);

        // GetCalendarKeys — return keys for the given household.
        mock.Setup(x => x.InvokeAsync<string[]>("window.happieCache.getCalendarKeys", It.IsAny<object[]>()))
            .Returns<string, object[]>((_, args) =>
            {
                var householdId = args[0]?.ToString() ?? string.Empty;
                var keys = store.Keys.Where(x => x.StartsWith($"{householdId}_")).ToArray();
                return ValueTask.FromResult(keys);
            });

        // PutCalendar — store the entry keyed by {householdId}_{month}.
        mock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "window.happieCache.putCalendar", It.IsAny<object[]>()))
            .Callback<string, object[]>((_, args) =>
            {
                var householdId = args[0]?.ToString() ?? string.Empty;
                var month = args[1]?.ToString() ?? string.Empty;
                var json = args[2]?.ToString() ?? string.Empty;
                var timestamp = Convert.ToInt64(args[3]);
                var key = $"{householdId}_{month}";
                store[key] = new CalendarEntry { Month = month, ResponseJson = json, Timestamp = timestamp };
            })
            .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)!);

        // DeleteCalendar — remove entry.
        mock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "window.happieCache.deleteCalendar", It.IsAny<object[]>()))
            .Callback<string, object[]>((_, args) =>
            {
                var householdId = args[0]?.ToString() ?? string.Empty;
                var month = args[1]?.ToString() ?? string.Empty;
                var key = $"{householdId}_{month}";
                store.Remove(key);
            })
            .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)!);

        // GetCalendar — retrieve the entry scoped by household.
        mock.Setup(x => x.InvokeAsync<CalendarEntry?>("window.happieCache.getCalendar", It.IsAny<object[]>()))
            .Returns<string, object[]>((_, args) =>
            {
                var householdId = args[0]?.ToString() ?? string.Empty;
                var month = args[1]?.ToString() ?? string.Empty;
                var key = $"{householdId}_{month}";
                store.TryGetValue(key, out var entry);
                return ValueTask.FromResult(entry);
            });

        return mock;
    }

    private static Mock<IJSRuntime> CreateMockJsRuntimeForMutationQueue(Dictionary<string, Queue<JsonElement>> store)
    {
        var mock = new Mock<IJSRuntime>();
        var idCounter = 1;

        // IsAvailable — returns true.
        mock.Setup(x => x.InvokeAsync<bool>("window.happieCache.isAvailable", It.IsAny<object[]>()))
            .ReturnsAsync(true);

        // Initialize — no-op.
        mock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "window.happieCache.initialize", It.IsAny<object[]>()))
            .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)!);

        // EnqueueMutation — store the mutation under the householdId key.
        mock.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "window.happieCache.enqueueMutation", It.IsAny<object[]>()))
            .Callback<string, object[]>((_, args) =>
            {
                var householdId = args[0]?.ToString() ?? string.Empty;
                var mutationObj = args[1];
                var json = JsonSerializer.Serialize(mutationObj);
                var element = JsonDocument.Parse(json).RootElement.Clone();

                // Add the id field to the stored element.
                var enriched = JsonDocument.Parse(
                    json.TrimEnd('}') + $",\"id\":{idCounter++}}}").RootElement.Clone();

                if (!store.ContainsKey(householdId))
                    store[householdId] = new Queue<JsonElement>();

                store[householdId].Enqueue(enriched);
            })
            .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)!);

        // DequeueMutation — dequeue from the household's queue.
        mock.Setup(x => x.InvokeAsync<JsonElement?>("window.happieCache.dequeueMutation", It.IsAny<object[]>()))
            .Returns<string, object[]>((_, args) =>
            {
                var householdId = args[0]?.ToString() ?? string.Empty;
                if (!store.ContainsKey(householdId) || store[householdId].Count == 0)
                    return new ValueTask<JsonElement?>((JsonElement?)null);

                var element = store[householdId].Dequeue();
                return new ValueTask<JsonElement?>((JsonElement?)element);
            });

        // PeekAllMutations — return all mutations for the household.
        mock.Setup(x => x.InvokeAsync<JsonElement>("window.happieCache.peekAllMutations", It.IsAny<object[]>()))
            .Returns<string, object[]>((_, args) =>
            {
                var householdId = args[0]?.ToString() ?? string.Empty;
                if (!store.ContainsKey(householdId) || store[householdId].Count == 0)
                {
                    var emptyArray = JsonDocument.Parse("[]").RootElement.Clone();
                    return new ValueTask<JsonElement>(emptyArray);
                }

                var arrayJson = $"[{string.Join(",", store[householdId].Select(x => x.GetRawText()))}]";
                var arrayElement = JsonDocument.Parse(arrayJson).RootElement.Clone();
                return new ValueTask<JsonElement>(arrayElement);
            });

        return mock;
    }

    // Internal DTOs matching CacheStore's private deserialization shape.
    private sealed class DayPlanEntry
    {
        public string Date { get; set; } = string.Empty;
        public string ResponseJson { get; set; } = string.Empty;
        public long Timestamp { get; set; }
    }

    private sealed class CalendarEntry
    {
        public string Month { get; set; } = string.Empty;
        public string ResponseJson { get; set; } = string.Empty;
        public long Timestamp { get; set; }
    }
}
