using Happie.Api.Infrastructure;
using Happie.Api.Infrastructure.Entities;
using Happie.Api.Infrastructure.Mappers;
using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for attendance records backed by Azure Table Storage.</summary>
public class AttendanceRepository : BaseRepository<AttendanceRecordEntity>, IAttendanceRepository
{
    private const string TableName = "AttendanceRecords";

    private readonly IAttendanceRecordMapper _mapper;

    /// <summary>Initializes a new instance of <see cref="AttendanceRepository"/>.</summary>
    public AttendanceRepository(ITableStorageClient client, IAttendanceRecordMapper mapper) : base(client, TableName)
    {
        _mapper = mapper;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AttendanceRecord>> GetByDateAsync(Guid householdId, DateOnly date, CancellationToken ct = default)
    {
        var entities = await QueryByRowKeyPrefixAsync(householdId.ToString(), $"{date:yyyy-MM-dd}_", ct);
        return entities.Select(x => _mapper.ToModel(householdId, x)).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AttendanceRecord>> GetByDateRangeAsync(Guid householdId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        // Fetch all records for the household and filter client-side by date range.
        // Row keys are formatted as "YYYY-MM-DD#HousemateId", so prefix filtering on date is not directly
        // possible for a range; we query the full partition and filter in memory.
        var all = await QueryByPartitionAsync(householdId.ToString(), ct);
        return all
            .Where(x =>
            {
                // The date portion is the first 10 characters of the row key.
                var datePart = x.RowKey[..10];
                return DateOnly.TryParse(datePart, out var d) && d >= from && d <= to;
            })
            .Select(x => _mapper.ToModel(householdId, x))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<AttendanceRecord?> GetAsync(Guid householdId, DateOnly date, Guid housemateId, CancellationToken ct = default)
    {
        var entity = await GetAsync(householdId.ToString(), $"{date:yyyy-MM-dd}_{housemateId}", ct);
        return entity is null ? null : _mapper.ToModel(householdId, entity);
    }

    /// <inheritdoc/>
    public Task UpsertAsync(AttendanceRecord record, CancellationToken ct = default)
        => UpsertAsync(_mapper.ToEntity(record), ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AttendanceRecord>> GetAllByHouseholdAsync(Guid householdId, CancellationToken ct = default)
    {
        var entities = await QueryByPartitionAsync(householdId.ToString(), ct);
        return entities.Select(x => _mapper.ToModel(householdId, x)).ToList();
    }
}
