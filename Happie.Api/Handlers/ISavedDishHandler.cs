using Happie.Api.Domain;
using Happie.Api.Results;

namespace Happie.Api.Handlers;

/// <summary>Handles saved dish management operations.</summary>
public interface ISavedDishHandler
{
    /// <summary>Returns all active (non-deleted) saved dishes for the household, sorted alphabetically.</summary>
    Task<IReadOnlyList<SavedDish>> GetAllActiveAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>Creates a new saved dish or reactivates a soft-deleted match.</summary>
    Task<SavedDishCreateResult> CreateAsync(Guid householdId, string description, CancellationToken cancellationToken = default);

    /// <summary>Updates the description of an existing saved dish.</summary>
    Task<SavedDishUpdateResult> UpdateAsync(Guid householdId, Guid savedDishId, string description, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a saved dish.</summary>
    Task<SavedDishDeleteResult> DeleteAsync(Guid householdId, Guid savedDishId, CancellationToken cancellationToken = default);

    /// <summary>Returns up to 5 recent custom dish descriptions that are not yet saved.</summary>
    Task<IReadOnlyList<string>> GetSuggestionsAsync(Guid householdId, CancellationToken cancellationToken = default);
}
