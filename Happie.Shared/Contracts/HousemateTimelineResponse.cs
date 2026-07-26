using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Response for the housemate timeline endpoint.</summary>
public record HousemateTimelineResponse(
    [property: JsonPropertyName("entries")] IReadOnlyList<HousemateTimelineDto> Entries,
    [property: JsonPropertyName("firstCookedDate")] string? FirstCookedDate);
