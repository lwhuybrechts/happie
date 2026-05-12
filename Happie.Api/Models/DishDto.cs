using System.Text.Json.Serialization;

namespace Happie.Api.Models;

/// <summary>The dish planned for a specific day, as returned in the day plan response.</summary>
public record DishDto(
    [property: JsonPropertyName("description")] string Description);
