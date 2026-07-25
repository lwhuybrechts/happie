using Happie.Api.Results;
using Happie.Shared.Contracts;

namespace Happie.Api.Handlers;

/// <summary>Handles housemate management requests.</summary>
public interface IHousemateHandler
{
    /// <summary>
    /// Returns all active (non-deleted) housemates for the given household.
    /// </summary>
    Task<IReadOnlyList<HousemateDto>> GetActiveHousematesAsync(Guid householdId, CancellationToken ct = default);

    /// <summary>
    /// Adds a new housemate with the given name to the household, auto-assigning the first unused palette color.
    /// Returns null if the name is invalid (empty, whitespace-only, or longer than 50 characters).
    /// </summary>
    Task<HousemateDto?> AddHousemateAsync(Guid householdId, string name, CancellationToken ct = default);

    /// <summary>
    /// Updates the name and/or color of an existing housemate.
    /// Either name or color (or both) may be provided; omitted fields are left unchanged.
    /// </summary>
    Task<UpdateHousemateResult> UpdateHousemateAsync(Guid householdId, Guid housemateId, string? name, string? color, CancellationToken ct = default);

    /// <summary>
    /// Deletes a housemate from the household.
    /// Hard-deletes if the housemate has no linked attendance records or comments; soft-deletes otherwise.
    /// Returns <see cref="DeleteHousemateOutcome.NotFound"/> if the housemate does not exist or is already deleted.
    /// </summary>
    Task<DeleteHousemateOutcome> DeleteHousemateAsync(Guid householdId, Guid housemateId, CancellationToken ct = default);

    /// <summary>
    /// Reorders housemates by assigning sort order based on the provided list of IDs.
    /// </summary>
    Task ReorderHousematesAsync(Guid householdId, List<Guid> orderedIds, CancellationToken ct = default);

    /// <summary>
    /// Reports the app version for a housemate. Trims the version string, rejects whitespace-only
    /// values, skips the local development version "1.0.0", and persists all other valid versions.
    /// </summary>
    Task<ReportVersionOutcome> ReportVersionAsync(Guid householdId, Guid housemateId, string version, CancellationToken cancellationToken = default);
}
