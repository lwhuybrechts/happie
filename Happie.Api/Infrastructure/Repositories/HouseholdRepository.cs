using Happie.Api.Infrastructure;
using Happie.Api.Infrastructure.Entities;
using Happie.Api.Infrastructure.Mappers;
using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for households backed by Azure Table Storage.</summary>
public class HouseholdRepository : BaseRepository<HouseholdEntity>, IHouseholdRepository
{
    private const string TableName = "Households";

    private readonly IHouseholdMapper _mapper;

    /// <summary>Initializes a new instance of <see cref="HouseholdRepository"/>.</summary>
    public HouseholdRepository(ITableStorageClient client, IHouseholdMapper mapper) : base(client, TableName)
    {
        _mapper = mapper;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Household>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await QueryByPartitionAsync("households", ct);
        return entities
            .Select(e => _mapper.ToModel(Guid.Parse(e.RowKey), e))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<Household?> GetAsync(Guid householdId, CancellationToken ct = default)
    {
        var entity = await GetAsync("households", householdId.ToString(), ct);
        return entity is null ? null : _mapper.ToModel(householdId, entity);
    }

    /// <inheritdoc/>
    public Task UpsertAsync(Household household, CancellationToken ct = default)
        => UpsertAsync(_mapper.ToEntity(household), ct);
}
