using Happie.Shared.Contracts;
using Happie.Shared.Domain;

namespace Happie.Web.Services.Caching;

/// <summary>Central API facade providing stale-while-revalidate caching for reads and offline queueing for writes.</summary>
public interface ICachedApiClient
{
    /// <summary>Raised when a background refresh returns a new DayPlanResponse that differs from the cached version.</summary>
    event Action<DayPlanResponse>? OnDayPlanUpdated;

    /// <summary>Raised when a background refresh returns a new CalendarResponse that differs from the cached version.</summary>
    event Action<CalendarResponse>? OnCalendarUpdated;

    /// <summary>Gets the day plan for the given date using stale-while-revalidate. Returns null only when offline with no cache.</summary>
    Task<DayPlanResponse?> GetDayPlanAsync(string date);

    /// <summary>Gets the calendar for the given month using stale-while-revalidate. Returns null only when offline with no cache.</summary>
    Task<CalendarResponse?> GetCalendarAsync(DateOnly viewedMonth);

    /// <summary>Saves an attendance change. Online: sends HTTP and updates cache. Offline: queues mutation and applies optimistically.</summary>
    Task<bool> SaveAttendanceAsync(string date, Guid housemateId, AttendanceStatus status);

    /// <summary>Saves a dish change. Online: sends HTTP and updates cache. Offline: queues mutation and applies optimistically.</summary>
    Task<bool> SaveDishAsync(string date, string description, int? dinnerTimeHour, int? dinnerTimeMinute, int timezoneOffsetMinutes);

    /// <summary>Deletes the dish for a given date. Online: sends HTTP and updates cache. Offline: queues mutation and applies optimistically.</summary>
    Task<bool> DeleteDishAsync(string date);

    /// <summary>Saves a chef status change. Online: sends HTTP and updates cache. Offline: queues mutation and applies optimistically.</summary>
    Task<bool> SaveChefStatusAsync(string date, Guid housemateId, bool isChef);

    /// <summary>Saves a comment. Online: sends HTTP and updates cache. Offline: queues mutation and applies optimistically.</summary>
    Task<bool> SaveCommentAsync(string date, Guid housemateId, string text);

    /// <summary>Deletes a comment. Online: sends HTTP and updates cache. Offline: queues mutation and applies optimistically.</summary>
    Task<bool> DeleteCommentAsync(string date, Guid housemateId);

    /// <summary>Whether the last GET operation resulted in a cold cache miss (no cached data available).</summary>
    bool IsColdCacheFetch { get; }

    /// <summary>Whether the last cold cache fetch failed.</summary>
    bool HasLoadError { get; }

    /// <summary>Retries the last failed cold cache fetch.</summary>
    Task<DayPlanResponse?> RetryDayPlanAsync(string date);

    /// <summary>Retries the last failed cold cache calendar fetch.</summary>
    Task<CalendarResponse?> RetryCalendarAsync(DateOnly viewedMonth);
}
