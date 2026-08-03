namespace Happie.Api.Domain;

/// <summary>Recipe summary metadata for a saved dish.</summary>
public record RecipeSummary(
    Guid HouseholdId,
    Guid SavedDishId,
    string? Summary,
    int? CookingDurationMinutes,
    int? Servings);
