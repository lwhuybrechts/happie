using System.Globalization;
using System.Text.Json;
using Happie.Api.Domain;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Results;
using Happie.Api.Services;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;
using Happie.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace Happie.Api.Handlers;

/// <summary>Handles push subscription and nudge operations.</summary>
public class PushHandler : IPushHandler
{
    private readonly IPushSubscriptionRepository _pushSubscriptionRepository;
    private readonly IHousemateRepository _housemateRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly SharedStringResolver _sharedStringResolver;
    private readonly ILogger<PushHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="PushHandler"/>.</summary>
    public PushHandler(
        IPushSubscriptionRepository pushSubscriptionRepository,
        IHousemateRepository housemateRepository,
        IPushNotificationService pushNotificationService,
        SharedStringResolver sharedStringResolver,
        ILogger<PushHandler> logger)
    {
        _pushSubscriptionRepository = pushSubscriptionRepository;
        _housemateRepository = housemateRepository;
        _pushNotificationService = pushNotificationService;
        _sharedStringResolver = sharedStringResolver;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task SubscribeAsync(Guid householdId, Guid housemateId, string endpoint, string p256dhKey, string authKey, Locale locale, CancellationToken ct = default)
    {
        // Remove any existing subscription with the same endpoint for a different housemate in this household.
        // This ensures only the active housemate on a device receives push notifications.
        var existingSubscriptions = await _pushSubscriptionRepository.GetAllAsync(householdId, ct);
        foreach (var existing in existingSubscriptions)
        {
            if (existing.Endpoint == endpoint && existing.HousemateId != housemateId)
                await _pushSubscriptionRepository.DeleteAsync(householdId, existing.HousemateId, ct);
        }

        var subscription = new Domain.PushSubscription(housemateId, householdId, endpoint, p256dhKey, authKey, locale);
        await _pushSubscriptionRepository.UpsertAsync(subscription, ct);
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
                : ResolveNudgeMessage(predefinedMessageKey!.Value, subscription.Locale, date);

            var payload = BuildNudgePayload(senderName, date, body, householdId);

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
    public async Task SendAutoNotificationsAsync(Guid householdId, Guid actorHousemateId, DateOnly date, string translationKey, string parameters, CancellationToken ct = default)
    {
        var subscriptions = await _pushSubscriptionRepository.GetAllAsync(householdId, ct);

        // Exclude the actor from recipients.
        var recipients = subscriptions.Where(x => x.HousemateId != actorHousemateId).ToList();

        // Fetch actor name and all housemates for resolving IDs in parameters.
        var housemates = await _housemateRepository.GetAllAsync(householdId, ct);
        var housemateById = housemates.ToDictionary(x => x.Id);
        var actorName = housemateById.TryGetValue(actorHousemateId, out var actor) ? actor.Name : string.Empty;

        // Resolve housemate IDs in parameters to current names before rendering notifications.
        var resolvedParameters = ParameterNameResolver.Resolve(parameters, housemateById);

        foreach (var subscription in recipients)
        {
            var body = _sharedStringResolver.Resolve(translationKey, resolvedParameters, subscription.Locale);
            var payload = BuildAutoNotificationPayload(actorName, date, body, householdId);

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

    /// <summary>Resolves a predefined nudge message key to a localized string using the shared resolver.</summary>
    private string ResolveNudgeMessage(NudgeMessageKey key, Locale locale, DateOnly date)
    {
        var nudgeKey = key switch
        {
            NudgeMessageKey.PleaseAddAttendance => TranslationKeys.NudgePleaseAddAttendance,
            NudgeMessageKey.WhatWouldYouLikeToEat => TranslationKeys.NudgeWhatWouldYouLikeToEat,
            NudgeMessageKey.DinnerSoonWhatsYourPlan => TranslationKeys.NudgeDinnerSoonWhatsYourPlan,
            _ => throw new InvalidOperationException($"Unhandled {nameof(NudgeMessageKey)}: {key}"),
        };

        var parameters = new Dictionary<string, string>
        {
            ["date"] = FormatDateForLocale(date, locale)
        };

        return _sharedStringResolver.Resolve(nudgeKey, parameters, locale);
    }

    /// <summary>Formats a date according to the target locale convention.</summary>
    private static string FormatDateForLocale(DateOnly date, Locale locale) =>
        locale switch
        {
            Locale.Nl => date.ToString("d MMMM", new CultureInfo("nl-NL")),
            Locale.En => date.ToString("MMMM d", new CultureInfo("en-US")),
            _ => throw new InvalidOperationException($"Unhandled {nameof(Locale)}: {locale}"),
        };

    /// <summary>Builds the JSON payload for a nudge push notification.</summary>
    private static string BuildNudgePayload(string senderName, DateOnly date, string body, Guid householdId)
    {
        var payload = new
        {
            title = senderName,
            body,
            data = new { url = $"/day/{date:yyyy-MM-dd}", householdId = householdId.ToString() },
        };

        return JsonSerializer.Serialize(payload);
    }

    /// <summary>Builds the JSON payload for an automatic day plan change notification.</summary>
    private static string BuildAutoNotificationPayload(string actorName, DateOnly date, string changeDescription, Guid householdId)
    {
        var payload = new
        {
            title = actorName,
            body = changeDescription,
            data = new { url = $"/day/{date:yyyy-MM-dd}", householdId = householdId.ToString() },
        };

        return JsonSerializer.Serialize(payload);
    }
}
