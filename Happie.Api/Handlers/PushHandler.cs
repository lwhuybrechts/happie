using System.Text.Json;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Results;
using Happie.Api.Services;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;
using Microsoft.Extensions.Logging;

namespace Happie.Api.Handlers;

/// <summary>Handles push subscription and nudge operations.</summary>
public class PushHandler : IPushHandler
{
    private readonly IPushSubscriptionRepository _pushSubscriptionRepository;
    private readonly IHousemateRepository _housemateRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly ILogger<PushHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="PushHandler"/>.</summary>
    public PushHandler(
        IPushSubscriptionRepository pushSubscriptionRepository,
        IHousemateRepository housemateRepository,
        IPushNotificationService pushNotificationService,
        ILogger<PushHandler> logger)
    {
        _pushSubscriptionRepository = pushSubscriptionRepository;
        _housemateRepository = housemateRepository;
        _pushNotificationService = pushNotificationService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task SubscribeAsync(Guid householdId, Guid housemateId, string endpoint, string p256dhKey, string authKey, Locale locale, CancellationToken ct = default)
    {
        var subscription = new Domain.PushSubscription(housemateId, householdId, endpoint, p256dhKey, authKey, locale);
        return _pushSubscriptionRepository.UpsertAsync(subscription, ct);
    }

    /// <inheritdoc/>
    public async Task<NudgeResult?> NudgeAsync(
        Guid householdId,
        Guid senderHousemateId,
        DateOnly date,
        IReadOnlyList<Guid> recipientIds,
        NudgeMessageKey? predefinedMessageKey,
        string? message,
        CancellationToken ct = default)
    {
        // Validate XOR: exactly one of predefinedMessageKey or message must be set.
        var hasPredefined = predefinedMessageKey.HasValue && predefinedMessageKey.Value != NudgeMessageKey.Custom;
        var hasMessage = message is not null;

        if (hasPredefined == hasMessage)
            return null;

        // Validate custom message length.
        if (hasMessage)
        {
            var trimmed = message!.Trim();
            if (trimmed.Length == 0 || trimmed.Length > 20)
                return null;

            message = trimmed;
        }

        // Fetch sender name for the payload.
        var sender = await _housemateRepository.GetAsync(householdId, senderHousemateId, ct);
        var senderName = sender?.Name ?? string.Empty;

        // Dispatch push to each recipient.
        var failures = new List<NudgeFailureDto>();

        foreach (var recipientId in recipientIds)
        {
            var subscription = await _pushSubscriptionRepository.GetAsync(householdId, recipientId, ct);

            if (subscription is null)
                continue;

            // Resolve message text in the recipient's locale.
            var body = hasMessage
                ? message!
                : NudgeMessageResolver.Resolve(predefinedMessageKey!.Value, subscription.Locale, date);

            var payload = BuildNudgePayload(senderName, date, body);

            try
            {
                await _pushNotificationService.SendAsync(subscription, payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deliver nudge to housemate {HousemateId}.", recipientId);
                failures.Add(new NudgeFailureDto(recipientId, ex.Message));
            }
        }

        return new NudgeResult(failures);
    }

    /// <inheritdoc/>
    public async Task SendAutoNotificationsAsync(Guid householdId, Guid actorHousemateId, DateOnly date, string changeDescription, CancellationToken ct = default)
    {
        var subscriptions = await _pushSubscriptionRepository.GetAllAsync(householdId, ct);

        // Exclude the actor from recipients.
        var recipients = subscriptions.Where(x => x.HousemateId != actorHousemateId).ToList();

        // Fetch actor name for the payload.
        var actor = await _housemateRepository.GetAsync(householdId, actorHousemateId, ct);
        var actorName = actor?.Name ?? string.Empty;

        foreach (var subscription in recipients)
        {
            var payload = BuildAutoNotificationPayload(actorName, date, changeDescription);

            try
            {
                await _pushNotificationService.SendAsync(subscription, payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deliver auto-notification to housemate {HousemateId}.", subscription.HousemateId);
            }
        }
    }

    /// <summary>Builds the JSON payload for a nudge push notification.</summary>
    private static string BuildNudgePayload(string senderName, DateOnly date, string body)
    {
        var payload = new
        {
            title = senderName,
            body,
            data = new { url = $"/day/{date:yyyy-MM-dd}" },
        };

        return JsonSerializer.Serialize(payload);
    }

    /// <summary>Builds the JSON payload for an automatic day plan change notification.</summary>
    private static string BuildAutoNotificationPayload(string actorName, DateOnly date, string changeDescription)
    {
        var payload = new
        {
            title = actorName,
            body = changeDescription,
            data = new { url = $"/day/{date:yyyy-MM-dd}" },
        };

        return JsonSerializer.Serialize(payload);
    }
}
