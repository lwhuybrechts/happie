using System.Text.Json.Serialization;

namespace Happie.Api.Models;

/// <summary>Response for the calendar date-range attendance summary endpoint.</summary>
public record CalendarResponse(
    [property: JsonPropertyName("days")] IReadOnlyList<CalendarDayDto> Days);
