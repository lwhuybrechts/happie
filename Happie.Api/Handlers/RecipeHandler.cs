using Happie.Api.Domain;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Results;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;

namespace Happie.Api.Handlers;

/// <summary>Handles recipe data operations for saved dishes.</summary>
public class RecipeHandler : IRecipeHandler
{
    private readonly ISavedDishRepository _savedDishRepository;
    private readonly IRecipeSummaryRepository _recipeSummaryRepository;
    private readonly IIngredientRepository _ingredientRepository;
    private readonly ICookingInstructionRepository _cookingInstructionRepository;
    private readonly IIngredientCheckRepository _ingredientCheckRepository;

    /// <summary>Initializes a new instance of <see cref="RecipeHandler"/>.</summary>
    public RecipeHandler(
        ISavedDishRepository savedDishRepository,
        IRecipeSummaryRepository recipeSummaryRepository,
        IIngredientRepository ingredientRepository,
        ICookingInstructionRepository cookingInstructionRepository,
        IIngredientCheckRepository ingredientCheckRepository)
    {
        _savedDishRepository = savedDishRepository;
        _recipeSummaryRepository = recipeSummaryRepository;
        _ingredientRepository = ingredientRepository;
        _cookingInstructionRepository = cookingInstructionRepository;
        _ingredientCheckRepository = ingredientCheckRepository;
    }

    /// <inheritdoc/>
    public async Task<RecipeSummaryResponse?> GetSummaryAsync(Guid householdId, Guid savedDishId, CancellationToken cancellationToken)
    {
        var dish = await _savedDishRepository.GetAsync(householdId, savedDishId, cancellationToken);
        if (dish is null || dish.IsDeleted)
            return null;

        var summary = await _recipeSummaryRepository.GetAsync(householdId, savedDishId, cancellationToken);
        if (summary is null)
            return new RecipeSummaryResponse(null, null, null);

        return new RecipeSummaryResponse(summary.Summary, summary.CookingDurationMinutes, summary.Servings);
    }

    /// <inheritdoc/>
    public async Task<IngredientsResponse?> GetIngredientsAsync(Guid householdId, Guid savedDishId, CancellationToken cancellationToken)
    {
        var dish = await _savedDishRepository.GetAsync(householdId, savedDishId, cancellationToken);
        if (dish is null || dish.IsDeleted)
            return null;

        var ingredients = await _ingredientRepository.GetAllAsync(householdId, savedDishId, cancellationToken);
        var checks = await _ingredientCheckRepository.GetAllAsync(householdId, savedDishId, cancellationToken);

        var ingredientDtos = ingredients
            .OrderBy(x => x.SortOrder)
            .Select(x => new IngredientDto(x.Id, x.Amount, x.Unit, x.Name, x.SortOrder))
            .ToList();

        var checkDtos = checks
            .Select(x => new IngredientCheckDto(x.IngredientId, x.IsChecked))
            .ToList();

        return new IngredientsResponse(ingredientDtos, checkDtos);
    }

    /// <inheritdoc/>
    public async Task<InstructionsResponse?> GetInstructionsAsync(Guid householdId, Guid savedDishId, CancellationToken cancellationToken)
    {
        var dish = await _savedDishRepository.GetAsync(householdId, savedDishId, cancellationToken);
        if (dish is null || dish.IsDeleted)
            return null;

        var instructions = await _cookingInstructionRepository.GetAllAsync(householdId, savedDishId, cancellationToken);

        var instructionDtos = instructions
            .OrderBy(x => x.SortOrder)
            .Select(x => new CookingInstructionDto(x.Id, x.Text, x.SortOrder))
            .ToList();

        return new InstructionsResponse(instructionDtos);
    }

    /// <inheritdoc/>
    public async Task<UpdateSummaryResult> UpdateSummaryAsync(Guid householdId, Guid savedDishId, UpdateSummaryRequest request, CancellationToken cancellationToken)
    {
        // Validate summary length.
        if (request.Summary is not null && request.Summary.Length > RecipeConstants.MaxSummaryLength)
            return new UpdateSummaryResult(UpdateSummaryOutcome.ValidationError);

        // Validate cooking duration is non-negative.
        if (request.CookingDurationMinutes is not null && request.CookingDurationMinutes < 0)
            return new UpdateSummaryResult(UpdateSummaryOutcome.ValidationError);

        // Validate servings range.
        if (request.Servings is not null && (request.Servings < RecipeConstants.MinServings || request.Servings > RecipeConstants.MaxServings))
            return new UpdateSummaryResult(UpdateSummaryOutcome.ValidationError);

        var dish = await _savedDishRepository.GetAsync(householdId, savedDishId, cancellationToken);
        if (dish is null || dish.IsDeleted)
            return new UpdateSummaryResult(UpdateSummaryOutcome.NotFound);

        var summary = new RecipeSummary(householdId, savedDishId, request.Summary, request.CookingDurationMinutes, request.Servings);
        await _recipeSummaryRepository.UpsertAsync(summary, cancellationToken);

        return new UpdateSummaryResult(UpdateSummaryOutcome.Success);
    }

    /// <inheritdoc/>
    public async Task<UpdateIngredientsResult> UpdateIngredientsAsync(Guid householdId, Guid savedDishId, UpdateIngredientsRequest request, CancellationToken cancellationToken)
    {
        // Validate max ingredients count.
        if (request.Ingredients.Count > RecipeConstants.MaxIngredients)
            return new UpdateIngredientsResult(UpdateIngredientsOutcome.ValidationError);

        // Validate each ingredient.
        foreach (var ingredient in request.Ingredients)
        {
            if (ingredient.Amount < RecipeConstants.MinIngredientAmount || ingredient.Amount > RecipeConstants.MaxIngredientAmount)
                return new UpdateIngredientsResult(UpdateIngredientsOutcome.ValidationError);

            if (ingredient.Name.Length > RecipeConstants.MaxIngredientNameLength)
                return new UpdateIngredientsResult(UpdateIngredientsOutcome.ValidationError);
        }

        var dish = await _savedDishRepository.GetAsync(householdId, savedDishId, cancellationToken);
        if (dish is null || dish.IsDeleted)
            return new UpdateIngredientsResult(UpdateIngredientsOutcome.NotFound);

        // Filter out ingredients with whitespace-only names (auto-delete).
        var validIngredients = request.Ingredients
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .ToList();

        // Get existing ingredients to determine which to delete.
        var existingIngredients = await _ingredientRepository.GetAllAsync(householdId, savedDishId, cancellationToken);
        var incomingIds = validIngredients.Select(x => x.Id).ToHashSet();
        var removedIngredients = existingIngredients
            .Where(x => !incomingIds.Contains(x.Id))
            .ToList();

        // Cascade delete ingredient checks for removed ingredients.
        if (removedIngredients.Count > 0)
        {
            var removedKeys = removedIngredients
                .Select(x => (x.SavedDishId, x.Id))
                .ToList();

            await _ingredientRepository.BatchDeleteAsync(householdId, removedKeys, cancellationToken);
            await _ingredientCheckRepository.BatchDeleteAsync(householdId, removedKeys, cancellationToken);
        }

        // Upsert valid ingredients.
        if (validIngredients.Count > 0)
        {
            var domainIngredients = validIngredients
                .Select(x => new Ingredient(x.Id, householdId, savedDishId, x.Amount, x.Unit, x.Name, x.SortOrder))
                .ToList();

            await _ingredientRepository.BatchUpsertAsync(domainIngredients, cancellationToken);
        }

        return new UpdateIngredientsResult(UpdateIngredientsOutcome.Success);
    }

    /// <inheritdoc/>
    public async Task<UpdateIngredientCheckResult> UpdateIngredientCheckAsync(Guid householdId, Guid savedDishId, Guid ingredientId, UpdateIngredientCheckRequest request, CancellationToken cancellationToken)
    {
        var dish = await _savedDishRepository.GetAsync(householdId, savedDishId, cancellationToken);
        if (dish is null || dish.IsDeleted)
            return new UpdateIngredientCheckResult(UpdateIngredientCheckOutcome.NotFound);

        var check = new IngredientCheck(householdId, savedDishId, ingredientId, request.IsChecked);
        await _ingredientCheckRepository.UpsertAsync(check, cancellationToken);

        return new UpdateIngredientCheckResult(UpdateIngredientCheckOutcome.Success);
    }

    /// <inheritdoc/>
    public async Task<UpdateInstructionsResult> UpdateInstructionsAsync(Guid householdId, Guid savedDishId, UpdateInstructionsRequest request, CancellationToken cancellationToken)
    {
        // Validate max instructions count.
        if (request.Instructions.Count > RecipeConstants.MaxInstructions)
            return new UpdateInstructionsResult(UpdateInstructionsOutcome.ValidationError);

        // Validate each instruction.
        foreach (var instruction in request.Instructions)
        {
            if (instruction.Text.Length > RecipeConstants.MaxInstructionTextLength)
                return new UpdateInstructionsResult(UpdateInstructionsOutcome.ValidationError);
        }

        var dish = await _savedDishRepository.GetAsync(householdId, savedDishId, cancellationToken);
        if (dish is null || dish.IsDeleted)
            return new UpdateInstructionsResult(UpdateInstructionsOutcome.NotFound);

        // Filter out instructions with whitespace-only text (auto-delete).
        var validInstructions = request.Instructions
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .ToList();

        // Get existing instructions to determine which to delete.
        var existingInstructions = await _cookingInstructionRepository.GetAllAsync(householdId, savedDishId, cancellationToken);
        var incomingIds = validInstructions.Select(x => x.Id).ToHashSet();
        var removedInstructions = existingInstructions
            .Where(x => !incomingIds.Contains(x.Id))
            .ToList();

        // Delete removed instructions.
        if (removedInstructions.Count > 0)
        {
            var removedKeys = removedInstructions
                .Select(x => (x.SavedDishId, x.Id))
                .ToList();

            await _cookingInstructionRepository.BatchDeleteAsync(householdId, removedKeys, cancellationToken);
        }

        // Upsert valid instructions.
        if (validInstructions.Count > 0)
        {
            var domainInstructions = validInstructions
                .Select(x => new CookingInstruction(x.Id, householdId, savedDishId, x.Text, x.SortOrder))
                .ToList();

            await _cookingInstructionRepository.BatchUpsertAsync(domainInstructions, cancellationToken);
        }

        return new UpdateInstructionsResult(UpdateInstructionsOutcome.Success);
    }
}
