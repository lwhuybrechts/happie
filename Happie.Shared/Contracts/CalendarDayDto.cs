using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Attendance summary for a single day in the calendar view.</summary>
public record CalendarDayDto(
    [property: JsonPropertyName("date")] DateOnly Date,
    [property: JsonPropertyName("eatingInColors")] IReadOnlyList<string> EatingInColors);
