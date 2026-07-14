using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>A saved dish as returned in the saved dishes list response.</summary>
public record SavedDishDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("description")] string Description);
