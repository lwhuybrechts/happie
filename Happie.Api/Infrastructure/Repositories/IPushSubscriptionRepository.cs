using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for push subscriptions.</summary>
public interface IPushSubscriptionRepository
{
    /// <summary>Gets all push subscriptions for a household.</summary>
    Task<IReadOnlyList<PushSubscription>> GetAllAsync(Guid householdId, CancellationToken ct = default);

    /// <summary>Gets the push subscription for a specific housemate, or null if not registered.</summary>
    Task<PushSubscription?> GetAsync(Guid householdId, Guid housemateId, CancellationToken ct = default);

    /// <summary>Upserts a push subscription.</summary>
    Task UpsertAsync(PushSubscription subscription, CancellationToken ct = default);

    /// <summary>Deletes the push subscription for a specific housemate.</summary>
    Task DeleteAsync(Guid householdId, Guid housemateId, CancellationToken ct = default);
}
