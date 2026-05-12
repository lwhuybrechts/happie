using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for dish records.</summary>
public interface IDishRepository
{
    /// <summary>Gets the dish record for a specific date in a household, or null if not set.</summary>
    Task<DishRecord?> GetAsync(Guid householdId, DateOnly date, CancellationToken ct = default);

    /// <summary>Upserts a dish record, attributing the change to the given housemate.</summary>
    Task UpsertAsync(DishRecord record, Guid lastChangedByHousemateId, CancellationToken ct = default);
}
