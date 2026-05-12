using Happie.Api.Infrastructure.Repositories;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Entities;

/// <summary>Azure Table Storage entity representing a VAPID Web Push subscription for a housemate.</summary>
public class PushSubscriptionEntity : MyTableEntity
{
    /// <summary>Parameterless constructor required for Azure Table Storage deserialization.</summary>
    public PushSubscriptionEntity() { }

    /// <summary>Initializes a new instance with the standard partition and row key for a push subscription.</summary>
    public PushSubscriptionEntity(Guid householdId, Guid housemateId)
    {
        PartitionKey = householdId.ToString();
        RowKey = housemateId.ToString();
    }

    /// <summary>The push service endpoint URL.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>The P-256 DH public key for payload encryption.</summary>
    public string P256dhKey { get; set; } = string.Empty;

    /// <summary>The authentication secret for payload encryption.</summary>
    public string AuthKey { get; set; } = string.Empty;

    /// <summary>The housemate's preferred locale for server-side message rendering.</summary>
    public Locale Locale { get; set; }
}
