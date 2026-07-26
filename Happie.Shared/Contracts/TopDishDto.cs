using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>A frequently cooked dish entry in the top dishes list.</summary>
public record TopDishDto(
    [property: JsonPropertyName("savedDishId")] Guid SavedDishId,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("count")] int Count);
