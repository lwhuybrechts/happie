using Happie.Api.Infrastructure;
using Happie.Api.Repositories.Entities;
using Happie.Api.Repositories.Mappers;
using Happie.Shared.Domain;

namespace Happie.Api.Repositories;

/// <summary>Repository for day history entries backed by Azure Table Storage.</summary>
public class DayHistoryRepository : BaseRepository<DayHistoryEntity>, IDayHistoryRepository
{
    private const string TableName = "DayHistory";

    private readonly IDayHistoryEntryMapper _mapper;

    /// <summary>Initializes a new instance of <see cref="DayHistoryRepository"/>.</summary>
    public DayHistoryRepository(ITableStorageClient client, IDayHistoryEntryMapper mapper) : base(client, TableName)
    {
        _mapper = mapper;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DayHistoryEntry>> GetByDateAsync(Guid householdId, DateOnly date, CancellationToken ct = default)
    {
        var entities = await QueryByRowKeyPrefixAsync(householdId.ToString(), $"{date:yyyy-MM-dd}#", ct);
        return entities.Select(e => _mapper.ToModel(householdId, date, e)).ToList();
    }

    /// <inheritdoc/>
    public Task AddAsync(DayHistoryEntry entry, CancellationToken ct = default)
        => UpsertAsync(_mapper.ToEntity(entry), ct);
}
