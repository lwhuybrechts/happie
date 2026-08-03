namespace Happie.Api.Domain;

/// <summary>A single cooking instruction step in a dish recipe.</summary>
public record CookingInstruction(
    Guid Id,
    Guid HouseholdId,
    Guid SavedDishId,
    string Text,
    int SortOrder);
