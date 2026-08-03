using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Response returned by GET /api/saved-dishes/{id}/ingredients.</summary>
public record IngredientsResponse(
    [property: JsonPropertyName("ingredients")] IReadOnlyList<IngredientDto> Ingredients,
    [property: JsonPropertyName("ingredientChecks")] IReadOnlyList<IngredientCheckDto> IngredientChecks);
