using Happie.Api.Domain;
using Happie.Api.Infrastructure.Entities;
using Happie.Api.Infrastructure.Mappers;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for day plan dish link records backed by Azure Table Storage.</summary>
public class DayPlanDishLinkRepository : BaseRepository<DayPlanDishLinkEntity>, IDayPlanDishLinkRepository
{
    private const string TableName = "DayPlanDishLinks";
    private readonly IDayPlanDishLinkMapper _mapper;

    /// <summary>Initializes a new instance of <see cref="DayPlanDishLinkRepository"/>.</summary>
    public DayPlanDishLinkRepository(ITableStorageClient client, IDayPlanDishLinkMapper mapper)
        : base(client, TableName)
    {
        _mapper = mapper;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DayPlanDishLink>> GetByDateAsync(Guid householdId, DateOnly date, CancellationToken ct = default)
    {
        var partitionKey = $"{householdId}_{date:yyyy-MM-dd}";
        var entities = await QueryByPartitionAsync(partitionKey, ct);
        return entities.Select(x => _mapper.ToModel(x)).OrderBy(x => x.SortOrder).ToList();
    }

    /// <inheritdoc/>
    public async Task ReplaceAllAsync(Guid householdId, DateOnly date, IReadOnlyList<DayPlanDishLink> links, CancellationToken ct = default)
    {
        await DeleteAllAsync(householdId, date, ct);
        foreach (var link in links)
            await UpsertAsync(_mapper.ToEntity(link), ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAllAsync(Guid householdId, DateOnly date, CancellationToken ct = default)
    {
        var partitionKey = $"{householdId}_{date:yyyy-MM-dd}";
        var existing = await QueryByPartitionAsync(partitionKey, ct);
        foreach (var entity in existing)
            await DeleteAsync(entity.PartitionKey, entity.RowKey, ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DayPlanDishLink>> GetAllByHouseholdAsync(Guid householdId, CancellationToken ct = default)
    {
        var prefix = $"{householdId}_";
        var entities = await QueryByPartitionPrefixAsync(prefix, ct);
        return entities.Select(x => _mapper.ToModel(x)).ToList();
    }

    /// <inheritdoc/>
    public Task CreateAsync(DayPlanDishLink link, CancellationToken ct = default)
        => UpsertAsync(_mapper.ToEntity(link), ct);
}
