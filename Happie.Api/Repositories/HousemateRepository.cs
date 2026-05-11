using Happie.Api.Infrastructure;
using Happie.Api.Repositories.Entities;
using Happie.Api.Repositories.Mappers;
using Happie.Shared.Domain;

namespace Happie.Api.Repositories;

/// <summary>Repository for housemates backed by Azure Table Storage.</summary>
public class HousemateRepository : BaseRepository<HousemateEntity>, IHousemateRepository
{
    private const string TableName = "Housemates";

    private readonly IHousemateMapper _mapper;

    /// <summary>Initializes a new instance of <see cref="HousemateRepository"/>.</summary>
    public HousemateRepository(ITableStorageClient client, IHousemateMapper mapper) : base(client, TableName)
    {
        _mapper = mapper;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Housemate>> GetAllAsync(Guid householdId, CancellationToken ct = default)
    {
        var entities = await QueryByPartitionAsync(householdId.ToString(), ct);
        return entities.Select(e => _mapper.ToModel(householdId, e)).ToList();
    }

    /// <inheritdoc/>
    public async Task<Housemate?> GetAsync(Guid householdId, Guid housemateId, CancellationToken ct = default)
    {
        var entity = await GetAsync(householdId.ToString(), housemateId.ToString(), ct);
        return entity is null ? null : _mapper.ToModel(householdId, entity);
    }

    /// <inheritdoc/>
    public Task UpsertAsync(Housemate housemate, CancellationToken ct = default)
        => UpsertAsync(_mapper.ToEntity(housemate), ct);

    /// <inheritdoc/>
    public Task DeleteAsync(Guid householdId, Guid housemateId, CancellationToken ct = default)
        => DeleteAsync(householdId.ToString(), housemateId.ToString(), ct);
}
