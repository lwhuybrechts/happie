using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>A suggested dish description that could be promoted to a saved dish.</summary>
public record SavedDishSuggestionDto(
    [property: JsonPropertyName("description")] string Description);
