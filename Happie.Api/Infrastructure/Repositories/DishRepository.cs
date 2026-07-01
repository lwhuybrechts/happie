using Happie.Api.Infrastructure;
using Happie.Api.Infrastructure.Entities;
using Happie.Api.Infrastructure.Mappers;
using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for dish records backed by Azure Table Storage.</summary>
public class DishRepository : BaseRepository<DishRecordEntity>, IDishRepository
{
    private const string TableName = "DishRecords";

    private readonly IDishRecordMapper _mapper;

    /// <summary>Initializes a new instance of <see cref="DishRepository"/>.</summary>
    public DishRepository(ITableStorageClient client, IDishRecordMapper mapper) : base(client, TableName)
    {
        _mapper = mapper;
    }

    /// <inheritdoc/>
    public async Task<DishRecord?> GetAsync(Guid householdId, DateOnly date, CancellationToken ct = default)
    {
        var entity = await GetAsync(householdId.ToString(), $"{date:yyyy-MM-dd}", ct);
        return entity is null ? null : _mapper.ToModel(householdId, date, entity);
    }

    /// <inheritdoc/>
    public Task UpsertAsync(DishRecord record, CancellationToken ct = default)
        => UpsertAsync(_mapper.ToEntity(record), ct);

    /// <inheritdoc/>
    public Task DeleteAsync(Guid householdId, DateOnly date, CancellationToken ct = default)
        => DeleteAsync(householdId.ToString(), $"{date:yyyy-MM-dd}", ct);
}
