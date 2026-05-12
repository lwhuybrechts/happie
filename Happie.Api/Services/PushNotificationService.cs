using Happie.Api.Domain;
using Happie.Api.Options;
using Microsoft.Extensions.Options;
using WebPush;

namespace Happie.Api.Services;

/// <summary>Sends VAPID Web Push notifications using the WebPush library.</summary>
public class PushNotificationService : IPushNotificationService
{
    private readonly VapidOptions _options;

    /// <summary>Initializes a new instance of <see cref="PushNotificationService"/>.</summary>
    public PushNotificationService(IOptions<VapidOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task SendAsync(Domain.PushSubscription subscription, string payload, CancellationToken ct = default)
    {
        var pushSubscription = new WebPush.PushSubscription(
            subscription.Endpoint,
            subscription.P256dhKey,
            subscription.AuthKey);

        var vapidDetails = new VapidDetails("mailto:admin@happie.app", _options.PublicKey, _options.PrivateKey);

        var client = new WebPushClient();
        await client.SendNotificationAsync(pushSubscription, payload, vapidDetails);
    }
}
