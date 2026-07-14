using Happie.Api.Constants;
using Happie.Api.Handlers;
using Happie.Api.Http;
using Happie.Api.Results;
using Happie.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Happie.Api.Functions;

/// <summary>Azure Function that handles saved dish management requests.</summary>
public class SavedDishesFunction
{
    private readonly ISavedDishHandler _savedDishHandler;

    /// <summary>Initializes a new instance of <see cref="SavedDishesFunction"/>.</summary>
    public SavedDishesFunction(ISavedDishHandler savedDishHandler)
    {
        _savedDishHandler = savedDishHandler;
    }

    /// <summary>Returns all active (non-deleted) saved dishes for the authenticated household, sorted alphabetically.</summary>
    [Function("GetSavedDishes")]
    public async Task<IActionResult> GetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "saved-dishes")] HttpRequest request,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        var dishes = await _savedDishHandler.GetAllActiveAsync(householdId, cancellationToken);
        var dtos = dishes.Select(x => new SavedDishDto(x.Id, x.Description)).ToList();

        return new OkObjectResult(dtos);
    }

    /// <summary>Creates a new saved dish or reactivates a soft-deleted match.</summary>
    [Function("CreateSavedDish")]
    public async Task<IActionResult> PostAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "saved-dishes")] HttpRequest request,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        var readResult = await RequestValidator.ReadAndValidateAsync<CreateSavedDishRequest>(request, cancellationToken);
        if (!readResult.IsSuccess)
            return readResult.Error;

        var result = await _savedDishHandler.CreateAsync(householdId, readResult.Body.Description, cancellationToken);

        return result.Outcome switch
        {
            SavedDishCreateOutcome.Created => new ObjectResult(new SavedDishDto(result.SavedDish!.Id, result.SavedDish.Description)) { StatusCode = StatusCodes.Status201Created },
            SavedDishCreateOutcome.Reactivated => new ObjectResult(new SavedDishDto(result.SavedDish!.Id, result.SavedDish.Description)) { StatusCode = StatusCodes.Status201Created },
            SavedDishCreateOutcome.AlreadyExists => new ObjectResult(new ApiErrorResponse("A saved dish with this description already exists.", ApiErrorCodes.DishAlreadyExists)) { StatusCode = StatusCodes.Status409Conflict },
            SavedDishCreateOutcome.ValidationError => new UnprocessableEntityObjectResult(new ApiErrorResponse("Description must be between 1 and 100 characters.", ApiErrorCodes.ValidationError)),
            _ => throw new InvalidOperationException($"Unhandled {nameof(SavedDishCreateOutcome)}: {result.Outcome}"),
        };
    }

    /// <summary>Updates the description of an existing saved dish.</summary>
    [Function("UpdateSavedDish")]
    public async Task<IActionResult> PutAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "saved-dishes/{id}")] HttpRequest request,
        string id,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        if (!Guid.TryParse(id, out var parsedId))
            return new BadRequestObjectResult(new ApiErrorResponse("Invalid saved dish ID.", ApiErrorCodes.BadRequest));

        var readResult = await RequestValidator.ReadAndValidateAsync<UpdateSavedDishRequest>(request, cancellationToken);
        if (!readResult.IsSuccess)
            return readResult.Error;

        var result = await _savedDishHandler.UpdateAsync(householdId, parsedId, readResult.Body.Description, cancellationToken);

        return result.Outcome switch
        {
            SavedDishUpdateOutcome.Updated => new OkObjectResult(new SavedDishDto(result.SavedDish!.Id, result.SavedDish.Description)),
            SavedDishUpdateOutcome.AlreadyExists => new ObjectResult(new ApiErrorResponse("A saved dish with this description already exists.", ApiErrorCodes.DishAlreadyExists)) { StatusCode = StatusCodes.Status409Conflict },
            SavedDishUpdateOutcome.NotFound => new NotFoundObjectResult(new ApiErrorResponse("Saved dish not found.", ApiErrorCodes.NotFound)),
            SavedDishUpdateOutcome.ValidationError => new UnprocessableEntityObjectResult(new ApiErrorResponse("Description must be between 1 and 100 characters.", ApiErrorCodes.ValidationError)),
            _ => throw new InvalidOperationException($"Unhandled {nameof(SavedDishUpdateOutcome)}: {result.Outcome}"),
        };
    }

    /// <summary>Soft-deletes a saved dish.</summary>
    [Function("DeleteSavedDish")]
    public async Task<IActionResult> DeleteAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "saved-dishes/{id}")] HttpRequest request,
        string id,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        if (!Guid.TryParse(id, out var parsedId))
            return new BadRequestObjectResult(new ApiErrorResponse("Invalid saved dish ID.", ApiErrorCodes.BadRequest));

        var outcome = await _savedDishHandler.DeleteAsync(householdId, parsedId, cancellationToken);

        return outcome switch
        {
            SavedDishDeleteResult.Deleted => new NoContentResult(),
            SavedDishDeleteResult.NotFound => new NotFoundObjectResult(new ApiErrorResponse("Saved dish not found.", ApiErrorCodes.NotFound)),
            _ => throw new InvalidOperationException($"Unhandled {nameof(SavedDishDeleteResult)}: {outcome}"),
        };
    }

    /// <summary>Returns up to 5 recent custom dish descriptions that are not yet saved.</summary>
    [Function("GetSavedDishSuggestions")]
    public async Task<IActionResult> GetSuggestionsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "saved-dishes/suggestions")] HttpRequest request,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        var suggestions = await _savedDishHandler.GetSuggestionsAsync(householdId, cancellationToken);
        var dtos = suggestions.Select(x => new SavedDishSuggestionDto(x)).ToList();

        return new OkObjectResult(dtos);
    }
}
