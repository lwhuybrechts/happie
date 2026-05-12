using System.ComponentModel.DataAnnotations;
using Happie.Shared.Domain;

namespace Happie.Shared.Contracts;

/// <summary>Request body for sending a nudge push notification to selected housemates.</summary>
public record NudgeRequest(
    [property: Required(ErrorMessage = "At least one recipient is required.")]
    [property: MinLength(1, ErrorMessage = "At least one recipient is required.")]
    IReadOnlyList<Guid> RecipientHousemateIds,
    NudgeMessageKey? PredefinedMessageKey,
    string? Message);
