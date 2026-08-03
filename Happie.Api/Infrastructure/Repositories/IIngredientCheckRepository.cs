using Happie.Api.Domain;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for ingredient check states.</summary>
public interface IIngredientCheckRepository
{
    /// <summary>Gets all ingredient checks for a saved dish.</summary>
    Task<IReadOnlyList<IngredientCheck>> GetAllAsync(Guid householdId, Guid savedDishId, CancellationToken cancellationToken = default);

    /// <summary>Upserts a single ingredient check.</summary>
    Task UpsertAsync(IngredientCheck check, CancellationToken cancellationToken = default);

    /// <summary>Deletes a single ingredient check.</summary>
    Task DeleteAsync(Guid householdId, Guid savedDishId, Guid ingredientId, CancellationToken cancellationToken = default);

    /// <summary>Deletes multiple ingredient checks by their keys.</summary>
    Task BatchDeleteAsync(Guid householdId, IReadOnlyList<(Guid SavedDishId, Guid IngredientId)> keys, CancellationToken cancellationToken = default);
}
