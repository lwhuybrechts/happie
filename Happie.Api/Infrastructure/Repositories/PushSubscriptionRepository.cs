using Happie.Api.Infrastructure;
using Happie.Api.Infrastructure.Entities;
using Happie.Api.Infrastructure.Mappers;
using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for push subscriptions backed by Azure Table Storage.</summary>
public class PushSubscriptionRepository : BaseRepository<PushSubscriptionEntity>, IPushSubscriptionRepository
{
    private const string TableName = "PushSubscriptions";

    private readonly IPushSubscriptionMapper _mapper;

    /// <summary>Initializes a new instance of <see cref="PushSubscriptionRepository"/>.</summary>
    public PushSubscriptionRepository(ITableStorageClient client, IPushSubscriptionMapper mapper) : base(client, TableName)
    {
        _mapper = mapper;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PushSubscription>> GetAllAsync(Guid householdId, CancellationToken ct = default)
    {
        var entities = await QueryByPartitionAsync(householdId.ToString(), ct);
        return entities.Select(x => _mapper.ToModel(householdId, x)).ToList();
    }

    /// <inheritdoc/>
    public async Task<PushSubscription?> GetAsync(Guid householdId, Guid housemateId, CancellationToken ct = default)
    {
        var entity = await GetAsync(householdId.ToString(), housemateId.ToString(), ct);
        return entity is null ? null : _mapper.ToModel(householdId, entity);
    }

    /// <inheritdoc/>
    public Task UpsertAsync(PushSubscription subscription, CancellationToken ct = default)
        => UpsertAsync(_mapper.ToEntity(subscription), ct);

    /// <inheritdoc/>
    public Task DeleteAsync(Guid householdId, Guid housemateId, CancellationToken ct = default)
        => DeleteAsync(householdId.ToString(), housemateId.ToString(), ct);
}
