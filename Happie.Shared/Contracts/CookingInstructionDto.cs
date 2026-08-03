using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>A single cooking instruction step in a recipe.</summary>
public record CookingInstructionDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("sortOrder")] int SortOrder);
