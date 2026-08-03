using Happie.Api.Constants;
using Happie.Api.Handlers;
using Happie.Api.Http;
using Happie.Api.Results;
using Happie.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Happie.Api.Functions;

/// <summary>Azure Function that handles recipe data requests for saved dishes.</summary>
public class RecipeFunction
{
    private readonly IRecipeHandler _recipeHandler;

    /// <summary>Initializes a new instance of <see cref="RecipeFunction"/>.</summary>
    public RecipeFunction(IRecipeHandler recipeHandler)
    {
        _recipeHandler = recipeHandler;
    }

    /// <summary>Returns the recipe summary for a saved dish.</summary>
    [Function("GetRecipeSummary")]
    public async Task<IActionResult> GetSummaryAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "saved-dishes/{id}/summary")] HttpRequest request,
        string id,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        if (!Guid.TryParse(id, out var savedDishId))
            return new BadRequestObjectResult(new ApiErrorResponse("Invalid saved dish ID.", ApiErrorCodes.BadRequest));

        var response = await _recipeHandler.GetSummaryAsync(householdId, savedDishId, cancellationToken);
        if (response is null)
            return new NotFoundObjectResult(new ApiErrorResponse("Saved dish not found.", ApiErrorCodes.NotFound));

        return new OkObjectResult(response);
    }

    /// <summary>Returns the ingredient list with check states for a saved dish.</summary>
    [Function("GetRecipeIngredients")]
    public async Task<IActionResult> GetIngredientsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "saved-dishes/{id}/ingredients")] HttpRequest request,
        string id,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        if (!Guid.TryParse(id, out var savedDishId))
            return new BadRequestObjectResult(new ApiErrorResponse("Invalid saved dish ID.", ApiErrorCodes.BadRequest));

        var response = await _recipeHandler.GetIngredientsAsync(householdId, savedDishId, cancellationToken);
        if (response is null)
            return new NotFoundObjectResult(new ApiErrorResponse("Saved dish not found.", ApiErrorCodes.NotFound));

        return new OkObjectResult(response);
    }

    /// <summary>Returns the cooking instructions for a saved dish.</summary>
    [Function("GetRecipeInstructions")]
    public async Task<IActionResult> GetInstructionsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "saved-dishes/{id}/instructions")] HttpRequest request,
        string id,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        if (!Guid.TryParse(id, out var savedDishId))
            return new BadRequestObjectResult(new ApiErrorResponse("Invalid saved dish ID.", ApiErrorCodes.BadRequest));

        var response = await _recipeHandler.GetInstructionsAsync(householdId, savedDishId, cancellationToken);
        if (response is null)
            return new NotFoundObjectResult(new ApiErrorResponse("Saved dish not found.", ApiErrorCodes.NotFound));

        return new OkObjectResult(response);
    }

    /// <summary>Updates the recipe summary for a saved dish.</summary>
    [Function("UpdateRecipeSummary")]
    public async Task<IActionResult> UpdateSummaryAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "saved-dishes/{id}/summary")] HttpRequest request,
        string id,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        if (!Guid.TryParse(id, out var savedDishId))
            return new BadRequestObjectResult(new ApiErrorResponse("Invalid saved dish ID.", ApiErrorCodes.BadRequest));

        var readResult = await RequestValidator.ReadAndValidateAsync<UpdateSummaryRequest>(request, cancellationToken);
        if (!readResult.IsSuccess)
            return readResult.Error;

        var result = await _recipeHandler.UpdateSummaryAsync(householdId, savedDishId, readResult.Body, cancellationToken);

        return result.Outcome switch
        {
            UpdateSummaryOutcome.Success => new OkResult(),
            UpdateSummaryOutcome.NotFound => new NotFoundObjectResult(new ApiErrorResponse("Saved dish not found.", ApiErrorCodes.NotFound)),
            UpdateSummaryOutcome.ValidationError => new UnprocessableEntityObjectResult(new ApiErrorResponse("Validation failed.", ApiErrorCodes.ValidationError)),
            _ => throw new InvalidOperationException($"Unhandled {nameof(UpdateSummaryOutcome)}: {result.Outcome}"),
        };
    }

    /// <summary>Batch-saves the ingredient list for a saved dish.</summary>
    [Function("UpdateRecipeIngredients")]
    public async Task<IActionResult> UpdateIngredientsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "saved-dishes/{id}/ingredients")] HttpRequest request,
        string id,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        if (!Guid.TryParse(id, out var savedDishId))
            return new BadRequestObjectResult(new ApiErrorResponse("Invalid saved dish ID.", ApiErrorCodes.BadRequest));

        var readResult = await RequestValidator.ReadAndValidateAsync<UpdateIngredientsRequest>(request, cancellationToken);
        if (!readResult.IsSuccess)
            return readResult.Error;

        var result = await _recipeHandler.UpdateIngredientsAsync(householdId, savedDishId, readResult.Body, cancellationToken);

        return result.Outcome switch
        {
            UpdateIngredientsOutcome.Success => new OkResult(),
            UpdateIngredientsOutcome.NotFound => new NotFoundObjectResult(new ApiErrorResponse("Saved dish not found.", ApiErrorCodes.NotFound)),
            UpdateIngredientsOutcome.ValidationError => new UnprocessableEntityObjectResult(new ApiErrorResponse("Validation failed.", ApiErrorCodes.ValidationError)),
            _ => throw new InvalidOperationException($"Unhandled {nameof(UpdateIngredientsOutcome)}: {result.Outcome}"),
        };
    }

    /// <summary>Toggles the checked state of a single ingredient.</summary>
    [Function("UpdateIngredientCheck")]
    public async Task<IActionResult> UpdateIngredientCheckAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "saved-dishes/{id}/ingredients/{ingredientId}/check")] HttpRequest request,
        string id,
        string ingredientId,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        if (!Guid.TryParse(id, out var savedDishId))
            return new BadRequestObjectResult(new ApiErrorResponse("Invalid saved dish ID.", ApiErrorCodes.BadRequest));

        if (!Guid.TryParse(ingredientId, out var parsedIngredientId))
            return new BadRequestObjectResult(new ApiErrorResponse("Invalid ingredient ID.", ApiErrorCodes.BadRequest));

        var readResult = await RequestValidator.ReadAndValidateAsync<UpdateIngredientCheckRequest>(request, cancellationToken);
        if (!readResult.IsSuccess)
            return readResult.Error;

        var result = await _recipeHandler.UpdateIngredientCheckAsync(householdId, savedDishId, parsedIngredientId, readResult.Body, cancellationToken);

        return result.Outcome switch
        {
            UpdateIngredientCheckOutcome.Success => new OkResult(),
            UpdateIngredientCheckOutcome.NotFound => new NotFoundObjectResult(new ApiErrorResponse("Saved dish not found.", ApiErrorCodes.NotFound)),
            _ => throw new InvalidOperationException($"Unhandled {nameof(UpdateIngredientCheckOutcome)}: {result.Outcome}"),
        };
    }

    /// <summary>Batch-saves the cooking instructions for a saved dish.</summary>
    [Function("UpdateRecipeInstructions")]
    public async Task<IActionResult> UpdateInstructionsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "saved-dishes/{id}/instructions")] HttpRequest request,
        string id,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        if (!Guid.TryParse(id, out var savedDishId))
            return new BadRequestObjectResult(new ApiErrorResponse("Invalid saved dish ID.", ApiErrorCodes.BadRequest));

        var readResult = await RequestValidator.ReadAndValidateAsync<UpdateInstructionsRequest>(request, cancellationToken);
        if (!readResult.IsSuccess)
            return readResult.Error;

        var result = await _recipeHandler.UpdateInstructionsAsync(householdId, savedDishId, readResult.Body, cancellationToken);

        return result.Outcome switch
        {
            UpdateInstructionsOutcome.Success => new OkResult(),
            UpdateInstructionsOutcome.NotFound => new NotFoundObjectResult(new ApiErrorResponse("Saved dish not found.", ApiErrorCodes.NotFound)),
            UpdateInstructionsOutcome.ValidationError => new UnprocessableEntityObjectResult(new ApiErrorResponse("Validation failed.", ApiErrorCodes.ValidationError)),
            _ => throw new InvalidOperationException($"Unhandled {nameof(UpdateInstructionsOutcome)}: {result.Outcome}"),
        };
    }
}
