using Happie.Shared.Domain;

namespace Happie.Api.Domain;

/// <summary>A single ingredient entry in a dish recipe.</summary>
public record Ingredient(
    Guid Id,
    Guid HouseholdId,
    Guid SavedDishId,
    double Amount,
    UnitOfMeasurement Unit,
    string Name,
    int SortOrder);
