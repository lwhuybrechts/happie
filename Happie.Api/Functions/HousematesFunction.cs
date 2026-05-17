using Happie.Api.Constants;
using Happie.Api.Handlers;
using Happie.Api.Http;
using Happie.Api.Results;
using Happie.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Happie.Api.Functions;

/// <summary>Azure Function that handles housemate management requests.</summary>
public class HousematesFunction
{
    private readonly IHousemateHandler _housemateHandler;

    /// <summary>Initializes a new instance of <see cref="HousematesFunction"/>.</summary>
    public HousematesFunction(IHousemateHandler housemateHandler)
    {
        _housemateHandler = housemateHandler;
    }

    /// <summary>Returns all active (non-deleted) housemates for the authenticated household.</summary>
    [Function("GetHousemates")]
    public async Task<IActionResult> GetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "housemates")] HttpRequest request,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        var housemates = await _housemateHandler.GetActiveHousematesAsync(householdId, cancellationToken);

        return new OkObjectResult(housemates);
    }

    /// <summary>Adds a new housemate to the authenticated household.</summary>
    [Function("AddHousemate")]
    public async Task<IActionResult> PostAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "housemates")] HttpRequest request,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        var readResult = await RequestValidator.ReadAndValidateAsync<AddHousemateRequest>(request, cancellationToken);
        if (!readResult.IsSuccess)
            return readResult.Error;

        var housemate = await _housemateHandler.AddHousemateAsync(householdId, readResult.Body.Name!, cancellationToken);

        if (housemate is null)
            return new UnprocessableEntityObjectResult(new ApiErrorResponse("Name must be between 1 and 50 characters.", ApiErrorCodes.ValidationError));

        return new ObjectResult(housemate) { StatusCode = StatusCodes.Status201Created };
    }

    /// <summary>Updates the name and/or color of an existing housemate.</summary>
    [Function("UpdateHousemate")]
    public async Task<IActionResult> PatchAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "housemates/{housemateId}")] HttpRequest request,
        string housemateId,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        if (!RouteParser.TryParseGuid(housemateId, out var parsedHousemateId, out var error))
            return error;

        var readResult = await RequestValidator.ReadAndValidateAsync<UpdateHousemateRequest>(request, cancellationToken);
        if (!readResult.IsSuccess)
            return readResult.Error;

        var updateResult = await _housemateHandler.UpdateHousemateAsync(householdId, parsedHousemateId, readResult.Body.Name, readResult.Body.Color, cancellationToken);

        return updateResult.Outcome switch
        {
            UpdateHousemateOutcome.Success => new OkObjectResult(updateResult.Housemate),
            UpdateHousemateOutcome.NotFound => new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound)),
            UpdateHousemateOutcome.ValidationError => new UnprocessableEntityObjectResult(new ApiErrorResponse(updateResult.ErrorMessage!, ApiErrorCodes.ValidationError)),
            UpdateHousemateOutcome.ColorConflict => new ConflictObjectResult(new ApiErrorResponse(updateResult.ErrorMessage!, ApiErrorCodes.ColorConflict)),
            _ => throw new InvalidOperationException($"Unhandled {nameof(UpdateHousemateOutcome)}: {updateResult.Outcome}"),
        };
    }

    /// <summary>Deletes a housemate from the authenticated household.</summary>
    [Function("DeleteHousemate")]
    public async Task<IActionResult> DeleteAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "housemates/{housemateId}")] HttpRequest request,
        string housemateId,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        if (!RouteParser.TryParseGuid(housemateId, out var parsedHousemateId, out var error))
            return error;

        var outcome = await _housemateHandler.DeleteHousemateAsync(householdId, parsedHousemateId, cancellationToken);

        return outcome switch
        {
            DeleteHousemateOutcome.Success => new NoContentResult(),
            DeleteHousemateOutcome.NotFound => new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound)),
            _ => throw new InvalidOperationException($"Unhandled {nameof(DeleteHousemateOutcome)}: {outcome}"),
        };
    }

    /// <summary>Reorders housemates within the authenticated household.</summary>
    [Function("ReorderHousemates")]
    public async Task<IActionResult> ReorderAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "housemates/order")] HttpRequest request,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        var readResult = await RequestValidator.ReadAndValidateAsync<ReorderHousematesRequest>(request, cancellationToken);
        if (!readResult.IsSuccess)
            return readResult.Error;

        await _housemateHandler.ReorderHousematesAsync(householdId, readResult.Body.OrderedIds, cancellationToken);

        return new NoContentResult();
    }
}
