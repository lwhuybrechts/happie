using Happie.Api.Repositories.Entities;
using Happie.Shared.Domain;

namespace Happie.Api.Repositories.Mappers;

/// <summary>Maps between <see cref="PushSubscriptionEntity"/> and <see cref="PushSubscription"/>.</summary>
public interface IPushSubscriptionMapper
{
    /// <summary>Maps a <see cref="PushSubscriptionEntity"/> to a <see cref="PushSubscription"/> domain record.</summary>
    PushSubscription ToModel(Guid householdId, PushSubscriptionEntity entity);

    /// <summary>Maps a <see cref="PushSubscription"/> domain record to a <see cref="PushSubscriptionEntity"/>.</summary>
    PushSubscriptionEntity ToEntity(PushSubscription subscription);
}
