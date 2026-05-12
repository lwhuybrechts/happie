using System.Text.Json.Serialization;
using Happie.Shared.Domain;

namespace Happie.Shared.Contracts;

/// <summary>Request body for the update attendance endpoint.</summary>
public record UpdateAttendanceRequest(
    [property: JsonPropertyName("status")] AttendanceStatus Status);
