using Happie.Api.Infrastructure;
using Happie.Api.Repositories.Entities;
using Happie.Api.Repositories.Mappers;
using Happie.Shared.Domain;

namespace Happie.Api.Repositories;

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
    public async Task<Household?> GetAsync(Guid householdId, CancellationToken ct = default)
    {
        var entity = await GetAsync("households", householdId.ToString(), ct);
        return entity is null ? null : _mapper.ToModel(householdId, entity);
    }

    /// <inheritdoc/>
    public Task UpsertAsync(Household household, CancellationToken ct = default)
        => UpsertAsync(_mapper.ToEntity(household), ct);
}
