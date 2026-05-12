using Happie.Shared.Domain;

namespace Happie.Api.Domain;

/// <summary>
/// A request to send a push notification reminder to selected housemates.
/// When <see cref="MessageKey"/> is <see cref="NudgeMessageKey.Custom"/>, <see cref="Message"/> must be set.
/// </summary>
public record NudgeRequest(
    Guid SenderHousemateId,
    DateOnly Date,
    IReadOnlyList<Guid> RecipientHousemateIds,
    NudgeMessageKey MessageKey,
    // Custom message text; required when MessageKey is Custom, max 20 chars.
    string? Message
);
