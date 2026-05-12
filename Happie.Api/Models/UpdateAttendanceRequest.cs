using Happie.Shared.Domain;

namespace Happie.Api.Models;

/// <summary>Request body for the update attendance endpoint.</summary>
public record UpdateAttendanceRequest(AttendanceStatus Status);
