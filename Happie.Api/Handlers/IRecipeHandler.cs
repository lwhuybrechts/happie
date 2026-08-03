using Happie.Api.Results;
using Happie.Shared.Contracts;

namespace Happie.Api.Handlers;

/// <summary>Handles recipe data operations for saved dishes.</summary>
public interface IRecipeHandler
{
    /// <summary>Gets the recipe summary for a saved dish, or null if the dish is not found.</summary>
    Task<RecipeSummaryResponse?> GetSummaryAsync(Guid householdId, Guid savedDishId, CancellationToken cancellationToken);

    /// <summary>Gets the ingredients and check states for a saved dish, or null if the dish is not found.</summary>
    Task<IngredientsResponse?> GetIngredientsAsync(Guid householdId, Guid savedDishId, CancellationToken cancellationToken);

    /// <summary>Gets the cooking instructions for a saved dish, or null if the dish is not found.</summary>
    Task<InstructionsResponse?> GetInstructionsAsync(Guid householdId, Guid savedDishId, CancellationToken cancellationToken);

    /// <summary>Updates the recipe summary for a saved dish.</summary>
    Task<UpdateSummaryResult> UpdateSummaryAsync(Guid householdId, Guid savedDishId, UpdateSummaryRequest request, CancellationToken cancellationToken);

    /// <summary>Updates the ingredient list for a saved dish.</summary>
    Task<UpdateIngredientsResult> UpdateIngredientsAsync(Guid householdId, Guid savedDishId, UpdateIngredientsRequest request, CancellationToken cancellationToken);

    /// <summary>Updates the check state of a single ingredient.</summary>
    Task<UpdateIngredientCheckResult> UpdateIngredientCheckAsync(Guid householdId, Guid savedDishId, Guid ingredientId, UpdateIngredientCheckRequest request, CancellationToken cancellationToken);

    /// <summary>Updates the cooking instructions for a saved dish.</summary>
    Task<UpdateInstructionsResult> UpdateInstructionsAsync(Guid householdId, Guid savedDishId, UpdateInstructionsRequest request, CancellationToken cancellationToken);
}
