using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Request body for PUT /api/saved-dishes/{id}/ingredients.</summary>
public record UpdateIngredientsRequest(
    [property: JsonPropertyName("ingredients")] IReadOnlyList<IngredientDto> Ingredients);
