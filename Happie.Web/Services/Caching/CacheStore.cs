using Microsoft.JSInterop;

namespace Happie.Web.Services.Caching;

/// <summary>Wraps JS interop calls to window.happieCache for IndexedDB caching.</summary>
public class CacheStore : ICacheStore
{
    private const int MaxDayPlanEntries = 30;
    private const int MaxCalendarEntries = 2;

    private readonly IJSRuntime _jsRuntime;
    private bool _isAvailable;

    public CacheStore(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("window.happieCache.initialize");
            _isAvailable = await _jsRuntime.InvokeAsync<bool>("window.happieCache.isAvailable");
        }
        catch (JSException)
        {
            _isAvailable = false;
        }
    }

    /// <inheritdoc />
    public async Task<CachedDayPlan?> GetDayPlanAsync(string householdId, string date)
    {
        if (!_isAvailable)
            return null;

        try
        {
            var entry = await _jsRuntime.InvokeAsync<DayPlanEntry?>("window.happieCache.getDayPlan", householdId, date);
            if (entry is null)
                return null;

            return new CachedDayPlan(entry.Date, entry.ResponseJson, entry.Timestamp);
        }
        catch (JSException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task PutDayPlanAsync(string householdId, string date, string responseJson)
    {
        if (!_isAvailable)
            return;

        try
        {
            var count = await _jsRuntime.InvokeAsync<int>("window.happieCache.getDayPlanCount", householdId);
            if (count >= MaxDayPlanEntries)
            {
                var oldestKey = await _jsRuntime.InvokeAsync<string?>("window.happieCache.getOldestDayPlanKey", householdId);
                if (oldestKey is not null)
                {
                    // Key format: {householdId}_{date} — extract the date part.
                    var separatorIndex = oldestKey.IndexOf('_');
                    if (separatorIndex >= 0)
                    {
                        var oldestDate = oldestKey[(separatorIndex + 1)..];
                        await _jsRuntime.InvokeVoidAsync("window.happieCache.deleteDayPlan", householdId, oldestDate);
                    }
                }
            }

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _jsRuntime.InvokeVoidAsync("window.happieCache.putDayPlan", householdId, date, responseJson, timestamp);
        }
        catch (JSException)
        {
            // IndexedDB operation failed; treat as unavailable gracefully.
        }
    }

    /// <inheritdoc />
    public async Task DeleteDayPlanAsync(string householdId, string date)
    {
        if (!_isAvailable)
            return;

        try
        {
            await _jsRuntime.InvokeVoidAsync("window.happieCache.deleteDayPlan", householdId, date);
        }
        catch (JSException)
        {
            // IndexedDB operation failed; treat as unavailable gracefully.
        }
    }

    /// <inheritdoc />
    public async Task<CachedCalendar?> GetCalendarAsync(string householdId, string month)
    {
        if (!_isAvailable)
            return null;

        try
        {
            var entry = await _jsRuntime.InvokeAsync<CalendarEntry?>("window.happieCache.getCalendar", householdId, month);
            if (entry is null)
                return null;

            return new CachedCalendar(entry.Month, entry.ResponseJson, entry.Timestamp);
        }
        catch (JSException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task PutCalendarAsync(string householdId, string month, string responseJson)
    {
        if (!_isAvailable)
            return;

        try
        {
            var existingKeys = await _jsRuntime.InvokeAsync<string[]>("window.happieCache.getCalendarKeys", householdId);
            var newKey = $"{householdId}_{month}";

            if (existingKeys.Length >= MaxCalendarEntries && !existingKeys.Contains(newKey))
            {
                // Determine the current month to preserve it.
                var currentMonth = DateTime.Now.ToString("yyyy-MM");
                var currentMonthKey = $"{householdId}_{currentMonth}";

                // Find the non-current-month entry to evict.
                var keyToEvict = existingKeys.FirstOrDefault(x => x != currentMonthKey);
                if (keyToEvict is not null)
                {
                    // Key format: {householdId}_{month} — extract the month part.
                    var separatorIndex = keyToEvict.IndexOf('_');
                    if (separatorIndex >= 0)
                    {
                        var monthToEvict = keyToEvict[(separatorIndex + 1)..];
                        await _jsRuntime.InvokeVoidAsync("window.happieCache.deleteCalendar", householdId, monthToEvict);
                    }
                }
            }

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _jsRuntime.InvokeVoidAsync("window.happieCache.putCalendar", householdId, month, responseJson, timestamp);
        }
        catch (JSException)
        {
            // IndexedDB operation failed; treat as unavailable gracefully.
        }
    }

    /// <inheritdoc />
    public async Task DeleteCalendarAsync(string householdId, string month)
    {
        if (!_isAvailable)
            return;

        try
        {
            await _jsRuntime.InvokeVoidAsync("window.happieCache.deleteCalendar", householdId, month);
        }
        catch (JSException)
        {
            // IndexedDB operation failed; treat as unavailable gracefully.
        }
    }

    /// <inheritdoc />
    public async Task ClearAllAsync(string householdId)
    {
        if (!_isAvailable)
            return;

        try
        {
            await _jsRuntime.InvokeVoidAsync("window.happieCache.clearAll", householdId);
        }
        catch (JSException)
        {
            // IndexedDB operation failed; treat as unavailable gracefully.
        }
    }

    /// <summary>Internal DTO for deserializing DayPlan entries from JS interop.</summary>
    private sealed class DayPlanEntry
    {
        public string Date { get; set; } = string.Empty;
        public string ResponseJson { get; set; } = string.Empty;
        public long Timestamp { get; set; }
    }

    /// <summary>Internal DTO for deserializing Calendar entries from JS interop.</summary>
    private sealed class CalendarEntry
    {
        public string Month { get; set; } = string.Empty;
        public string ResponseJson { get; set; } = string.Empty;
        public long Timestamp { get; set; }
    }
}
