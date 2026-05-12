using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>The full day plan for a specific date, as returned by GET /api/days/{date}.</summary>
public record DayPlanResponse(
    [property: JsonPropertyName("date")] DateOnly Date,
    [property: JsonPropertyName("dish")] DishDto? Dish,
    [property: JsonPropertyName("attendance")] IReadOnlyList<AttendanceDto> Attendance,
    [property: JsonPropertyName("comments")] IReadOnlyList<CommentDto> Comments,
    [property: JsonPropertyName("history")] IReadOnlyList<HistoryEntryDto> History);
