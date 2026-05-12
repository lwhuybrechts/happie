using System.Text.Json.Serialization;
using Happie.Shared.Domain;

namespace Happie.Shared.Contracts;

/// <summary>The attendance status of a housemate for a specific day, as returned in the day plan response.</summary>
public record AttendanceDto(
    [property: JsonPropertyName("housemateId")] Guid HousemateId,
    [property: JsonPropertyName("housemateName")] string HousemateName,
    [property: JsonPropertyName("color")] string Color,
    [property: JsonPropertyName("status")] AttendanceStatus Status);
