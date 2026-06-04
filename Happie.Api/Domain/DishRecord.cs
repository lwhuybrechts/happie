namespace Happie.Api.Domain;

/// <summary>The dish planned for a specific day in a household.</summary>
public record DishRecord(
    Guid HouseholdId,
    DateOnly Date,
    // Max 100 chars.
    string Description,
    Guid? LastChangedByHousemateId,
    DateTimeOffset? LastChangedAt,
    TimeOnly? DinnerTime
);
