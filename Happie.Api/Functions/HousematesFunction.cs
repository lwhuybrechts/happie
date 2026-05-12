using Happie.Api.Constants;
using Happie.Api.Handlers;
using Happie.Api.Models;
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "housemates")] HttpRequest req,
        FunctionContext context,
        CancellationToken ct)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        var housemates = await _housemateHandler.GetActiveHousematesAsync(householdId, ct);

        return new OkObjectResult(housemates);
    }

    /// <summary>Adds a new housemate to the authenticated household.</summary>
    [Function("AddHousemate")]
    public async Task<IActionResult> PostAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "housemates")] HttpRequest req,
        FunctionContext context,
        CancellationToken ct)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        AddHousemateRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<AddHousemateRequest>(ct);
        }
        catch
        {
            return new BadRequestObjectResult(new ApiErrorResponse("Invalid request body.", ApiErrorCodes.BadRequest));
        }

        if (body is null || body.Name is null)
            return new UnprocessableEntityObjectResult(new ApiErrorResponse("Name is required.", ApiErrorCodes.ValidationError));

        var result = await _housemateHandler.AddHousemateAsync(householdId, body.Name, ct);

        if (result is null)
            return new UnprocessableEntityObjectResult(new ApiErrorResponse("Name must be between 1 and 50 characters.", ApiErrorCodes.ValidationError));

        return new ObjectResult(result) { StatusCode = StatusCodes.Status201Created };
    }

    /// <summary>Updates the name and/or color of an existing housemate.</summary>
    [Function("UpdateHousemate")]
    public async Task<IActionResult> PatchAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "housemates/{housemateId}")] HttpRequest req,
        string housemateId,
        FunctionContext context,
        CancellationToken ct)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        if (!Guid.TryParse(housemateId, out var parsedHousemateId))
            return new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound));

        UpdateHousemateRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<UpdateHousemateRequest>(ct);
        }
        catch
        {
            return new BadRequestObjectResult(new ApiErrorResponse("Invalid request body.", ApiErrorCodes.BadRequest));
        }

        if (body is null)
            return new UnprocessableEntityObjectResult(new ApiErrorResponse("Request body is required.", ApiErrorCodes.ValidationError));

        var result = await _housemateHandler.UpdateHousemateAsync(householdId, parsedHousemateId, body.Name, body.Color, ct);

        return result.Outcome switch
        {
            UpdateHousemateOutcome.Success => new OkObjectResult(result.Housemate),
            UpdateHousemateOutcome.NotFound => new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound)),
            UpdateHousemateOutcome.ValidationError => new UnprocessableEntityObjectResult(new ApiErrorResponse(result.ErrorMessage!, ApiErrorCodes.ValidationError)),
            UpdateHousemateOutcome.ColorConflict => new ConflictObjectResult(new ApiErrorResponse(result.ErrorMessage!, ApiErrorCodes.ColorConflict)),
            _ => throw new InvalidOperationException($"Unhandled {nameof(UpdateHousemateOutcome)}: {result.Outcome}"),
        };
    }

    /// <summary>Deletes a housemate from the authenticated household.</summary>
    [Function("DeleteHousemate")]
    public async Task<IActionResult> DeleteAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "housemates/{housemateId}")] HttpRequest req,
        string housemateId,
        FunctionContext context,
        CancellationToken ct)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        if (!Guid.TryParse(housemateId, out var parsedHousemateId))
            return new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound));

        var outcome = await _housemateHandler.DeleteHousemateAsync(householdId, parsedHousemateId, ct);

        return outcome switch
        {
            DeleteHousemateOutcome.Success => new NoContentResult(),
            DeleteHousemateOutcome.NotFound => new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound)),
            _ => throw new InvalidOperationException($"Unhandled {nameof(DeleteHousemateOutcome)}: {outcome}"),
        };
    }
}
