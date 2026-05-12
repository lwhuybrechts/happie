using Happie.Api.Domain;

namespace Happie.Api.Services;

/// <summary>Sends VAPID Web Push notifications to housemates.</summary>
public interface IPushNotificationService
{
    /// <summary>
    /// Sends a push notification to the given subscription with the specified payload.
    /// Throws on delivery failure.
    /// </summary>
    Task SendAsync(PushSubscription subscription, string payload, CancellationToken ct = default);
}
