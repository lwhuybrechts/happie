using Happie.Shared.Contracts;
using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.Handlers;

/// <summary>Handles day plan requests.</summary>
public interface IDayHandler
{
    /// <summary>
    /// Returns the full day plan for the given household and date.
    /// Attendance defaults to <c>Unknown</c> for active housemates with no record.
    /// Soft-deleted housemate names are formatted as "Name (deleted)" in historical data.
    /// </summary>
    Task<DayPlanResponse> GetDayPlanAsync(Guid householdId, DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// Upserts the attendance status for a housemate on a given date and writes a history entry.
    /// Returns <c>false</c> if the housemate does not exist in the household.
    /// </summary>
    Task<bool> UpsertAttendanceAsync(Guid householdId, DateOnly date, Guid housemateId, AttendanceStatus status, Guid actingHousemateId, CancellationToken ct = default);

    /// <summary>
    /// Upserts the dish description for a household on a given date and writes a history entry.
    /// </summary>
    Task UpsertDishAsync(Guid householdId, DateOnly date, string description, TimeOnly? dinnerTime, int timezoneOffsetMinutes, Guid actingHousemateId, CancellationToken ct = default);

    /// <summary>
    /// Deletes the dish for a household on a given date and writes a history entry.
    /// </summary>
    Task DeleteDishAsync(Guid householdId, DateOnly date, Guid actingHousemateId, CancellationToken ct = default);

    /// <summary>
    /// Upserts the comment for a housemate on a given date and writes a history entry.
    /// Returns <c>false</c> if the housemate does not exist in the household.
    /// </summary>
    Task<bool> UpsertCommentAsync(Guid householdId, DateOnly date, Guid housemateId, string text, Guid actingHousemateId, CancellationToken ct = default);

    /// <summary>
    /// Deletes the comment for a housemate on a given date and writes a history entry.
    /// Returns <c>false</c> if the housemate does not exist in the household.
    /// </summary>
    Task<bool> DeleteCommentAsync(Guid householdId, DateOnly date, Guid housemateId, Guid actingHousemateId, CancellationToken ct = default);

    /// <summary>
    /// Upserts the chef status for a housemate on a given date and writes a history entry.
    /// Returns <c>false</c> if the housemate does not exist or is soft-deleted.
    /// </summary>
    Task<bool> UpsertChefStatusAsync(Guid householdId, DateOnly date, Guid housemateId, bool isChef, Guid actingHousemateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns attendance summaries (housemate color + status) for all days in the given date range.
    /// Only housemates with <c>EatingIn</c> status contribute a color to each day's summary.
    /// </summary>
    Task<CalendarResponse> GetCalendarAsync(Guid householdId, DateOnly from, DateOnly to, CancellationToken ct = default);
}
