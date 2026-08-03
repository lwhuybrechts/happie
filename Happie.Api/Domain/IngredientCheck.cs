namespace Happie.Api.Domain;

/// <summary>Tracks whether an ingredient has been checked off for a dish.</summary>
public record IngredientCheck(
    Guid HouseholdId,
    Guid SavedDishId,
    Guid IngredientId,
    bool IsChecked);
