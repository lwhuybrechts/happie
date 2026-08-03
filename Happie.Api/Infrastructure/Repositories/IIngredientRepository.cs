using Happie.Api.Domain;

namespace Happie.Api.Infrastructure.Repositories;

/// <summary>Repository for recipe ingredients.</summary>
public interface IIngredientRepository
{
    /// <summary>Gets all ingredients for a saved dish.</summary>
    Task<IReadOnlyList<Ingredient>> GetAllAsync(Guid householdId, Guid savedDishId, CancellationToken cancellationToken = default);

    /// <summary>Upserts a single ingredient.</summary>
    Task UpsertAsync(Ingredient ingredient, CancellationToken cancellationToken = default);

    /// <summary>Deletes a single ingredient.</summary>
    Task DeleteAsync(Guid householdId, Guid savedDishId, Guid ingredientId, CancellationToken cancellationToken = default);

    /// <summary>Upserts multiple ingredients.</summary>
    Task BatchUpsertAsync(IReadOnlyList<Ingredient> ingredients, CancellationToken cancellationToken = default);

    /// <summary>Deletes multiple ingredients by their keys.</summary>
    Task BatchDeleteAsync(Guid householdId, IReadOnlyList<(Guid SavedDishId, Guid IngredientId)> keys, CancellationToken cancellationToken = default);
}
