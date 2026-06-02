using Happie.Api.Results;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;

namespace Happie.Api.Handlers;

/// <summary>Handles push subscription and nudge operations.</summary>
public interface IPushHandler
{
    /// <summary>
    /// Upserts the push subscription for the given housemate.
    /// </summary>
    Task SubscribeAsync(Guid householdId, Guid housemateId, string endpoint, string p256dhKey, string authKey, Locale locale, CancellationToken ct = default);

    /// <summary>
    /// Sends a nudge push notification to the specified recipients for the given date.
    /// Returns per-recipient failures; delivery to other recipients continues even when one fails.
    /// Returns null when validation fails (invalid message combination or length).
    /// </summary>
    Task<NudgeResult?> NudgeAsync(Guid householdId, Guid senderHousemateId, DateOnly date, IReadOnlyList<Guid> recipientIds, NudgeMessageKey? predefinedMessageKey, string? message, CancellationToken ct = default);

    /// <summary>
    /// Sends automatic push notifications to all active housemates except the actor
    /// after a day plan change for today or tomorrow.
    /// Push failures are logged but do not interrupt the save.
    /// </summary>
    Task SendAutoNotificationsAsync(Guid householdId, Guid actorHousemateId, DateOnly date, string translationKey, string parameters, CancellationToken ct = default);
}
