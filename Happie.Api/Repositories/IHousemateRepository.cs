using Happie.Shared.Domain;

namespace Happie.Api.Repositories;

/// <summary>Repository for housemates.</summary>
public interface IHousemateRepository
{
    /// <summary>Gets all housemates in a household.</summary>
    Task<IReadOnlyList<Housemate>> GetAllAsync(Guid householdId, CancellationToken ct = default);

    /// <summary>Gets a single housemate by household and housemate ID, or null if not found.</summary>
    Task<Housemate?> GetAsync(Guid householdId, Guid housemateId, CancellationToken ct = default);

    /// <summary>Upserts a housemate.</summary>
    Task UpsertAsync(Housemate housemate, CancellationToken ct = default);

    /// <summary>Deletes a housemate.</summary>
    Task DeleteAsync(Guid householdId, Guid housemateId, CancellationToken ct = default);
}
