using Happie.Shared.Domain;

namespace Happie.Api.Domain;

/// <summary>An audit log entry recording a change made to a day plan.</summary>
public record DayHistoryEntry(
    Guid HouseholdId,
    DateOnly Date,
    DateTimeOffset ChangedAt,
    Guid ChangedByHousemateId,
    ChangeType ChangeType,
    // Human-readable summary of the change.
    string Description
);
