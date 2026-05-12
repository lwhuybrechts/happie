using Happie.Shared.Domain;

namespace Happie.Shared.Contracts;

/// <summary>Request body for sending a nudge push notification to selected housemates.</summary>
public record NudgeRequest(
    IReadOnlyList<Guid> RecipientHousemateIds,
    NudgeMessageKey? PredefinedMessageKey,
    string? Message
);
