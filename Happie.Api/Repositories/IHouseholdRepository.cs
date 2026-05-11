using Happie.Shared.Domain;

namespace Happie.Api.Repositories;

/// <summary>Repository for households.</summary>
public interface IHouseholdRepository
{
    /// <summary>Gets a household by its ID, or null if not found.</summary>
    Task<Household?> GetAsync(Guid householdId, CancellationToken ct = default);

    /// <summary>Upserts a household.</summary>
    Task UpsertAsync(Household household, CancellationToken ct = default);
}
