using System.Text.Json.Serialization;
using Happie.Shared.Domain;

namespace Happie.Shared.Contracts;

/// <summary>A single ingredient in a recipe's ingredient list.</summary>
public record IngredientDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("amount")] double Amount,
    [property: JsonPropertyName("unit")] UnitOfMeasurement Unit,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("sortOrder")] int SortOrder);
