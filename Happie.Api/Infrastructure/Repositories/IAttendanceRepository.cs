using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for attendance records.</summary>
public interface IAttendanceRepository
{
    /// <summary>Gets all attendance records for a household on a specific date.</summary>
    Task<IReadOnlyList<AttendanceRecord>> GetByDateAsync(Guid householdId, DateOnly date, CancellationToken ct = default);

    /// <summary>Gets all attendance records for a household within a date range.</summary>
    Task<IReadOnlyList<AttendanceRecord>> GetByDateRangeAsync(Guid householdId, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Gets a single attendance record for a housemate on a specific date, or null if not found.</summary>
    Task<AttendanceRecord?> GetAsync(Guid householdId, DateOnly date, Guid housemateId, CancellationToken ct = default);

    /// <summary>Upserts an attendance record.</summary>
    Task UpsertAsync(AttendanceRecord record, CancellationToken ct = default);

    /// <summary>Gets all attendance records for a household across all dates.</summary>
    Task<IReadOnlyList<AttendanceRecord>> GetAllByHouseholdAsync(Guid householdId, CancellationToken ct = default);
}
