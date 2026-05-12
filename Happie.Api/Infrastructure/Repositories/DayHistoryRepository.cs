using Happie.Api.Infrastructure;
using Happie.Api.Infrastructure.Entities;
using Happie.Api.Infrastructure.Mappers;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Repositories;

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
        var entities = await QueryByRowKeyPrefixAsync(householdId.ToString(), $"{date:yyyy-MM-dd}_", ct);
        return entities.Select(x => _mapper.ToModel(householdId, date, x)).ToList();
    }

    /// <inheritdoc/>
    public Task AddAsync(DayHistoryEntry entry, CancellationToken ct = default)
        => UpsertAsync(_mapper.ToEntity(entry), ct);
}
