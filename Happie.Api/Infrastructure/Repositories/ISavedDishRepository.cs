using Happie.Api.Domain;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for saved dishes.</summary>
public interface ISavedDishRepository
{
    /// <summary>Gets all saved dishes in a household.</summary>
    Task<IReadOnlyList<SavedDish>> GetAllAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>Gets a single saved dish by household and saved dish ID, or null if not found.</summary>
    Task<SavedDish?> GetAsync(Guid householdId, Guid savedDishId, CancellationToken cancellationToken = default);

    /// <summary>Upserts a saved dish.</summary>
    Task UpsertAsync(SavedDish savedDish, CancellationToken cancellationToken = default);
}
