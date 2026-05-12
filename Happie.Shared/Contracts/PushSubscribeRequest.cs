using System.ComponentModel.DataAnnotations;
using Happie.Shared.Domain;

namespace Happie.Shared.Contracts;

/// <summary>Request body for registering or renewing a VAPID Web Push subscription.</summary>
public record PushSubscribeRequest(
    [property: Required(ErrorMessage = "Endpoint is required.")]
    string Endpoint,
    [property: Required(ErrorMessage = "P256dhKey is required.")]
    string P256dhKey,
    [property: Required(ErrorMessage = "AuthKey is required.")]
    string AuthKey,
    Locale Locale);
