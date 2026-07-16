namespace Happie.Api.Domain;

/// <summary>Represents the association between a day plan and a saved dish.</summary>
public record DayPlanDishLink(
    Guid HouseholdId,
    DateOnly Date,
    Guid SavedDishId,
    int SortOrder);
