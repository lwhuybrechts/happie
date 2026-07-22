using Happie.Shared.Contracts;
using Happie.Shared.Domain;

namespace Happie.Web.Services.Caching;

/// <summary>Central API facade providing stale-while-revalidate caching for reads and offline queueing for writes.</summary>
public interface ICachedApiClient
{
    /// <summary>Raised when a background refresh returns a new DayPlanResponse that differs from the cached version.</summary>
    event Action<DayPlanResponse>? OnDayPlanUpdated;

    /// <summary>Raised when a background refresh returns a new CalendarResponse that differs from the cached version.</summary>
    event Action<DateOnly, CalendarResponse>? OnCalendarUpdated;

    /// <summary>Raised when a background refresh returns a new saved dishes list that differs from the cached version.</summary>
    event Action<IReadOnlyList<SavedDishDto>>? OnSavedDishesUpdated;

    /// <summary>Whether the last fetch was a cold cache fetch (no cached data available).</summary>
    bool IsColdCacheFetch { get; }

    /// <summary>Whether the last fetch resulted in a load error.</summary>
    bool HasLoadError { get; }

    /// <summary>Gets the day plan for the given date using stale-while-revalidate. Returns a result record with cached data and an optional background refresh task.</summary>
    Task<DayPlanFetchResult> GetDayPlanAsync(string date);

    /// <summary>Gets the calendar for the given month using stale-while-revalidate. Returns a result record with cached data and an optional background refresh task.</summary>
    Task<CalendarFetchResult> GetCalendarAsync(DateOnly viewedMonth);

    /// <summary>Saves an attendance change. Online: sends HTTP and updates cache. Offline: queues mutation and applies optimistically.</summary>
    Task<bool> SaveAttendanceAsync(string date, Guid housemateId, AttendanceStatus status);

    /// <summary>Saves a dish change. Online: sends HTTP and updates cache. Offline: queues mutation and applies optimistically.</summary>
    Task<bool> SaveDishAsync(string date, string? description, int? dinnerTimeHour, int? dinnerTimeMinute, int timezoneOffsetMinutes, IReadOnlyList<Guid>? savedDishIds = null, string? resolvedDescription = null);

    /// <summary>Deletes the dish for a given date. Online: sends HTTP and updates cache. Offline: queues mutation and applies optimistically.</summary>
    Task<bool> DeleteDishAsync(string date);

    /// <summary>Saves a chef status change. Online: sends HTTP and updates cache. Offline: queues mutation and applies optimistically.</summary>
    Task<bool> SaveChefStatusAsync(string date, Guid housemateId, bool isChef);

    /// <summary>Saves a comment. Online: sends HTTP and updates cache. Offline: queues mutation and applies optimistically.</summary>
    Task<bool> SaveCommentAsync(string date, Guid housemateId, string text);

    /// <summary>Deletes a comment. Online: sends HTTP and updates cache. Offline: queues mutation and applies optimistically.</summary>
    Task<bool> DeleteCommentAsync(string date, Guid housemateId);

    /// <summary>Gets the saved dishes list using stale-while-revalidate. Returns cached data immediately if available.</summary>
    Task<SavedDishesFetchResult> GetSavedDishesAsync();

    /// <summary>Refetches the saved dishes list from the API and replaces the cache entry. Called after successful mutations. If the refetch fails, deletes the cache entry so the next access triggers a fresh fetch.</summary>
    Task RefreshSavedDishesCacheAsync();

    /// <summary>Retries the last failed cold cache fetch.</summary>
    Task<DayPlanResponse?> RetryDayPlanAsync(string date);

    /// <summary>Retries the last failed cold cache calendar fetch.</summary>
    Task<CalendarResponse?> RetryCalendarAsync(DateOnly viewedMonth);
}
