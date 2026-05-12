using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for day history entries.</summary>
public interface IDayHistoryRepository
{
    /// <summary>Gets all history entries for a household on a specific date, in reverse-chronological order.</summary>
    Task<IReadOnlyList<DayHistoryEntry>> GetByDateAsync(Guid householdId, DateOnly date, CancellationToken ct = default);

    /// <summary>Appends a new history entry.</summary>
    Task AddAsync(DayHistoryEntry entry, CancellationToken ct = default);
}
