namespace Happie.Migration;

/// <summary>Parsed components of an old-format DayPlanDishLink record.</summary>
public record OldFormatRecord(Guid HouseholdId, DateOnly Date, Guid SavedDishId, int SortOrder);
