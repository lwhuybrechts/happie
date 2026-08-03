using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Request body for PUT /api/saved-dishes/{id}/ingredients/{ingredientId}/check.</summary>
public record UpdateIngredientCheckRequest(
    [property: JsonPropertyName("isChecked")] bool IsChecked);
