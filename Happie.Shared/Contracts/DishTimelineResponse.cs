using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Response for the dish timeline endpoint.</summary>
public record DishTimelineResponse(
    [property: JsonPropertyName("entries")] IReadOnlyList<DishTimelineDto> Entries,
    [property: JsonPropertyName("firstCookedDate")] string? FirstCookedDate);
