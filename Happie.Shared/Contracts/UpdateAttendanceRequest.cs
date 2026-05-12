using System.Text.Json.Serialization;
using Happie.Shared.Domain;
using Happie.Shared.Validation;

namespace Happie.Shared.Contracts;

/// <summary>Request body for the update attendance endpoint.</summary>
public record UpdateAttendanceRequest(
    [property: JsonPropertyName("status")]
    [property: ValidEnum(ErrorMessage = "Invalid attendance status.")]
    AttendanceStatus Status);
