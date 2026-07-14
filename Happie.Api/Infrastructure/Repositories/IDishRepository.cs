using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for dish records.</summary>
public interface IDishRepository
{
    /// <summary>Gets the dish record for a specific date in a household, or null if not set.</summary>
    Task<DishRecord?> GetAsync(Guid householdId, DateOnly date, CancellationToken ct = default);

    /// <summary>Upserts a dish record.</summary>
    Task UpsertAsync(DishRecord record, CancellationToken ct = default);

    /// <summary>Deletes the dish record for a specific date in a household.</summary>
    Task DeleteAsync(Guid householdId, DateOnly date, CancellationToken ct = default);

    /// <summary>Gets all dish records for a household.</summary>
    Task<IReadOnlyList<DishRecord>> GetAllByPartitionAsync(Guid householdId, CancellationToken ct = default);
}
