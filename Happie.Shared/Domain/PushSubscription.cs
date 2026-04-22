namespace Happie.Shared.Domain;

/// <summary>A VAPID Web Push subscription for a housemate, used to deliver nudges and auto-notifications.</summary>
public record PushSubscription(
    Guid HousemateId,
    Guid HouseholdId,
    // Push service URL.
    string Endpoint,
    string P256dhKey,
    string AuthKey,
    // Used to render predefined nudge messages in the recipient's language.
    Locale Locale
);
