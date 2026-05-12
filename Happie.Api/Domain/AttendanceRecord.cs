using Happie.Shared.Domain;

namespace Happie.Api.Domain;

/// <summary>The attendance status of a housemate for a specific day.</summary>
public record AttendanceRecord(
    Guid HouseholdId,
    Guid HousemateId,
    DateOnly Date,
    AttendanceStatus Status
);
