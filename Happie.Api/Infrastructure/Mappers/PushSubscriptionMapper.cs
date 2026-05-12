using Happie.Api.Infrastructure.Entities;
using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="PushSubscriptionEntity"/> and <see cref="PushSubscription"/>.</summary>
public class PushSubscriptionMapper : IPushSubscriptionMapper
{
    /// <inheritdoc/>
    public PushSubscription ToModel(Guid householdId, PushSubscriptionEntity entity) =>
        new(Guid.Parse(entity.RowKey), householdId, entity.Endpoint, entity.P256dhKey, entity.AuthKey, entity.Locale);

    /// <inheritdoc/>
    public PushSubscriptionEntity ToEntity(PushSubscription subscription)
    {
        var entity = new PushSubscriptionEntity(subscription.HouseholdId, subscription.HousemateId);
        entity.Endpoint = subscription.Endpoint;
        entity.P256dhKey = subscription.P256dhKey;
        entity.AuthKey = subscription.AuthKey;
        entity.Locale = subscription.Locale;
        return entity;
    }
}
