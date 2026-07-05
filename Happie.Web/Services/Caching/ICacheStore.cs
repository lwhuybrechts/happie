namespace Happie.Web.Services.Caching;

/// <summary>Abstracts IndexedDB cache operations for DayPlan and Calendar responses.</summary>
public interface ICacheStore
{
    /// <summary>Initializes the IndexedDB database and checks availability.</summary>
    Task InitializeAsync();

    /// <summary>Gets a cached DayPlan entry, or null if not found or unavailable.</summary>
    Task<CachedDayPlan?> GetDayPlanAsync(string householdId, string date);

    /// <summary>Stores a DayPlan entry, evicting the oldest if the 30-entry limit is reached.</summary>
    Task PutDayPlanAsync(string householdId, string date, string responseJson);

    /// <summary>Deletes a cached DayPlan entry.</summary>
    Task DeleteDayPlanAsync(string householdId, string date);

    /// <summary>Gets a cached Calendar entry, or null if not found or unavailable.</summary>
    Task<CachedCalendar?> GetCalendarAsync(string householdId, string month);

    /// <summary>Stores a Calendar entry, enforcing the 6-entry limit per household with cluster-based protection.</summary>
    Task PutCalendarAsync(string householdId, string month, string responseJson, string viewedMonth);

    /// <summary>Deletes a cached Calendar entry.</summary>
    Task DeleteCalendarAsync(string householdId, string month);

    /// <summary>Clears all cache and mutation queue entries for the given household.</summary>
    Task ClearAllAsync(string householdId);
}
