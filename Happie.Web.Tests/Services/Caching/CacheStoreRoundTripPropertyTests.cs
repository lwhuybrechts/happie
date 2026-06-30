using System.Text.Json;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services.Caching;
using Microsoft.JSInterop;

namespace Happie.Web.Tests.Services.Caching;

// Feature: offline-cache, Property 1: Cache round-trip preserves data
public class CacheStoreRoundTripPropertyTests
{
    private static readonly Arbitrary<string> NonEmptyJsonArb =
        Gen.Elements(
                "{\"attendance\":[{\"id\":\"abc\",\"status\":\"EatingIn\"}]}",
                "{\"dish\":\"Pasta Carbonara\",\"comments\":[]}",
                "{\"days\":[{\"date\":\"2024-01-15\",\"eatingIn\":[\"#FF0000\"]}]}",
                "{\"empty\":true}",
                "{\"nested\":{\"a\":{\"b\":{\"c\":1}}}}",
                "{\"special\":\"chars: \\\"quotes\\\" and \\\\backslash\"}",
                "{\"unicode\":\"héllo wörld 日本語\"}",
                "{\"large_array\":[1,2,3,4,5,6,7,8,9,10]}")
            .SelectMany(x => Gen.Elements("a", "b", "c", "extra", "field")
                .Select(suffix => x.Replace("}", $",\"{suffix}\":\"{Guid.NewGuid()}\"}}")))
            .ToArbitrary();

    private static readonly Arbitrary<string> HouseholdIdArb =
        Gen.Elements(
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString())
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

    // Feature: offline-cache, Property 1: Cache round-trip preserves data
    // Validates: Requirements 1.1, 2.1, 5.1, 5.2
    [Property(MaxTest = 100)]
    public Property PutDayPlan_ThenGetDayPlan_ReturnsIdenticalJson()
    {
        return Prop.ForAll(
            HouseholdIdArb,
            DateArb,
            NonEmptyJsonArb,
            (householdId, date, responseJson) =>
            {
                var jsRuntime = new FakeJsRuntime();
                var sut = CreateInitializedCacheStore(jsRuntime);

                sut.PutDayPlanAsync(householdId, date, responseJson).GetAwaiter().GetResult();
                var result = sut.GetDayPlanAsync(householdId, date).GetAwaiter().GetResult();

                return (result is not null && result.ResponseJson == responseJson)
                    .Label($"Expected round-trip to preserve JSON. Got: {result?.ResponseJson ?? "null"}");
            });
    }

    // Feature: offline-cache, Property 1: Cache round-trip preserves data
    // Validates: Requirements 1.1, 2.1, 5.1, 5.2
    [Property(MaxTest = 100)]
    public Property PutCalendar_ThenGetCalendar_ReturnsIdenticalJson()
    {
        return Prop.ForAll(
            HouseholdIdArb,
            MonthArb,
            NonEmptyJsonArb,
            (householdId, month, responseJson) =>
            {
                var jsRuntime = new FakeJsRuntime();
                var sut = CreateInitializedCacheStore(jsRuntime);

                sut.PutCalendarAsync(householdId, month, responseJson).GetAwaiter().GetResult();
                var result = sut.GetCalendarAsync(householdId, month).GetAwaiter().GetResult();

                return (result is not null && result.ResponseJson == responseJson)
                    .Label($"Expected round-trip to preserve JSON. Got: {result?.ResponseJson ?? "null"}");
            });
    }

    private static CacheStore CreateInitializedCacheStore(IJSRuntime jsRuntime)
    {
        var cacheStore = new CacheStore(jsRuntime);
        cacheStore.InitializeAsync().GetAwaiter().GetResult();
        return cacheStore;
    }

    /// <summary>
    /// Fake IJSRuntime that simulates an in-memory IndexedDB store.
    /// Handles all window.happieCache.* calls by storing/retrieving from dictionaries.
    /// Returns deserialized objects matching CacheStore's private DTO shapes.
    /// </summary>
    private sealed class FakeJsRuntime : IJSRuntime
    {
        private readonly Dictionary<string, DayPlanData> _dayPlanStore = new();
        private readonly Dictionary<string, CalendarData> _calendarStore = new();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            object? result = identifier switch
            {
                "window.happieCache.initialize" => null,
                "window.happieCache.isAvailable" => true,
                "window.happieCache.getDayPlan" => GetDayPlan(args),
                "window.happieCache.putDayPlan" => PutDayPlan(args),
                "window.happieCache.getDayPlanCount" => GetDayPlanCount(args),
                "window.happieCache.getOldestDayPlanKey" => GetOldestDayPlanKey(args),
                "window.happieCache.deleteDayPlan" => DeleteDayPlan(args),
                "window.happieCache.getCalendar" => GetCalendar(args),
                "window.happieCache.putCalendar" => PutCalendar(args),
                "window.happieCache.getCalendarKeys" => GetCalendarKeys(args),
                "window.happieCache.deleteCalendar" => DeleteCalendar(args),
                "window.happieCache.clearAll" => ClearAll(args),
                _ => default(TValue)
            };

            // For void-returning calls, result is null.
            if (result is null && typeof(TValue).Name.Contains("IJSVoidResult"))
                return default;

            // Handle type conversion for deserialization.
            if (result is null)
                return ValueTask.FromResult(default(TValue)!);

            // For bool, int, string, string[] — direct cast.
            if (result is TValue typedResult)
                return ValueTask.FromResult(typedResult);

            // For complex objects (DayPlanEntry, CalendarEntry), serialize and
            // deserialize to match CacheStore's expected private DTO shape.
            var json = JsonSerializer.Serialize(result);
            var deserialized = JsonSerializer.Deserialize<TValue>(json);
            return ValueTask.FromResult(deserialized!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, args);
        }

        private object? GetDayPlan(object?[]? args)
        {
            var householdId = args?[0]?.ToString() ?? string.Empty;
            var date = args?[1]?.ToString() ?? string.Empty;
            var key = $"{householdId}_{date}";

            if (_dayPlanStore.TryGetValue(key, out var entry))
                return entry;

            return null;
        }

        private object? PutDayPlan(object?[]? args)
        {
            var householdId = args?[0]?.ToString() ?? string.Empty;
            var date = args?[1]?.ToString() ?? string.Empty;
            var responseJson = args?[2]?.ToString() ?? string.Empty;
            var timestamp = Convert.ToInt64(args?[3]);
            var key = $"{householdId}_{date}";

            _dayPlanStore[key] = new DayPlanData
            {
                Date = date,
                ResponseJson = responseJson,
                Timestamp = timestamp
            };
            return null;
        }

        private object? GetDayPlanCount(object?[]? args)
        {
            var householdId = args?[0]?.ToString() ?? string.Empty;
            return _dayPlanStore.Count(x => x.Key.StartsWith($"{householdId}_"));
        }

        private object? GetOldestDayPlanKey(object?[]? args)
        {
            var householdId = args?[0]?.ToString() ?? string.Empty;
            var oldest = _dayPlanStore
                .Where(x => x.Key.StartsWith($"{householdId}_"))
                .OrderBy(x => x.Value.Timestamp)
                .Select(x => x.Key)
                .FirstOrDefault();
            return oldest;
        }

        private object? DeleteDayPlan(object?[]? args)
        {
            var householdId = args?[0]?.ToString() ?? string.Empty;
            var date = args?[1]?.ToString() ?? string.Empty;
            var key = $"{householdId}_{date}";
            _dayPlanStore.Remove(key);
            return null;
        }

        private object? GetCalendar(object?[]? args)
        {
            var householdId = args?[0]?.ToString() ?? string.Empty;
            var month = args?[1]?.ToString() ?? string.Empty;
            var key = $"{householdId}_{month}";

            if (_calendarStore.TryGetValue(key, out var entry))
                return entry;

            return null;
        }

        private object? PutCalendar(object?[]? args)
        {
            var householdId = args?[0]?.ToString() ?? string.Empty;
            var month = args?[1]?.ToString() ?? string.Empty;
            var responseJson = args?[2]?.ToString() ?? string.Empty;
            var timestamp = Convert.ToInt64(args?[3]);
            var key = $"{householdId}_{month}";

            _calendarStore[key] = new CalendarData
            {
                Month = month,
                ResponseJson = responseJson,
                Timestamp = timestamp
            };
            return null;
        }

        private object? GetCalendarKeys(object?[]? args)
        {
            var householdId = args?[0]?.ToString() ?? string.Empty;
            return _calendarStore.Keys
                .Where(x => x.StartsWith($"{householdId}_"))
                .ToArray();
        }

        private object? DeleteCalendar(object?[]? args)
        {
            var householdId = args?[0]?.ToString() ?? string.Empty;
            var month = args?[1]?.ToString() ?? string.Empty;
            var key = $"{householdId}_{month}";
            _calendarStore.Remove(key);
            return null;
        }

        private object? ClearAll(object?[]? args)
        {
            var householdId = args?[0]?.ToString() ?? string.Empty;
            var dayPlanKeys = _dayPlanStore.Keys
                .Where(x => x.StartsWith($"{householdId}_"))
                .ToList();
            foreach (var key in dayPlanKeys)
                _dayPlanStore.Remove(key);

            var calendarKeys = _calendarStore.Keys
                .Where(x => x.StartsWith($"{householdId}_"))
                .ToList();
            foreach (var key in calendarKeys)
                _calendarStore.Remove(key);

            return null;
        }
    }

    // Internal data classes matching CacheStore's private deserialization shape.
    private sealed class DayPlanData
    {
        public string Date { get; set; } = string.Empty;
        public string ResponseJson { get; set; } = string.Empty;
        public long Timestamp { get; set; }
    }

    private sealed class CalendarData
    {
        public string Month { get; set; } = string.Empty;
        public string ResponseJson { get; set; } = string.Empty;
        public long Timestamp { get; set; }
    }
}
