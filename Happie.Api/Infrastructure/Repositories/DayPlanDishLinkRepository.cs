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
    public async Task<IReadOnlyList<DayPlanDishLink>> GetByDateAsync(Guid householdId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var entities = await QueryByRowKeyPrefixAsync(householdId.ToString(), $"{date:yyyy-MM-dd}_", cancellationToken);
        return entities.Select(x => _mapper.ToModel(x)).OrderBy(x => x.SortOrder).ToList();
    }

    /// <inheritdoc/>
    public async Task ReplaceAllAsync(Guid householdId, DateOnly date, IReadOnlyList<DayPlanDishLink> links, CancellationToken cancellationToken = default)
    {
        await DeleteAllAsync(householdId, date, cancellationToken);
        foreach (var link in links)
            await UpsertAsync(_mapper.ToEntity(link), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAllAsync(Guid householdId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var entities = await QueryByRowKeyPrefixAsync(householdId.ToString(), $"{date:yyyy-MM-dd}_", cancellationToken);
        foreach (var entity in entities)
            await DeleteAsync(entity.PartitionKey, entity.RowKey, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DayPlanDishLink>> GetAllByHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var entities = await QueryByPartitionAsync(householdId.ToString(), cancellationToken);
        return entities.Select(x => _mapper.ToModel(x)).ToList();
    }

    /// <inheritdoc/>
    public Task CreateAsync(DayPlanDishLink link, CancellationToken cancellationToken = default)
        => UpsertAsync(_mapper.ToEntity(link), cancellationToken);
}
