using Happie.Shared.Domain;

namespace Happie.Shared.Contracts;

/// <summary>Request body for registering or renewing a VAPID Web Push subscription.</summary>
public record PushSubscribeRequest(
    string Endpoint,
    string P256dhKey,
    string AuthKey,
    Locale Locale
);
