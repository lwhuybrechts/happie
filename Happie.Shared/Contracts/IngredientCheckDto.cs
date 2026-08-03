using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>The checked state of a single ingredient in a recipe.</summary>
public record IngredientCheckDto(
    [property: JsonPropertyName("ingredientId")] Guid IngredientId,
    [property: JsonPropertyName("isChecked")] bool IsChecked);
