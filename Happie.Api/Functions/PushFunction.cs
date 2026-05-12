using Happie.Api.Constants;
using Happie.Api.Handlers;
using Happie.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Happie.Api.Functions;

/// <summary>Azure Function that handles push subscription and nudge requests.</summary>
public class PushFunction
{
    private readonly IPushHandler _pushHandler;

    /// <summary>Initializes a new instance of <see cref="PushFunction"/>.</summary>
    public PushFunction(IPushHandler pushHandler)
    {
        _pushHandler = pushHandler;
    }

    /// <summary>Registers or renews the push subscription for the active housemate.</summary>
    [Function("PushSubscribe")]
    public async Task<IActionResult> SubscribeAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "push/subscribe")] HttpRequest req,
        FunctionContext context,
        CancellationToken ct)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];
        var housemateId = (Guid)context.Items[FunctionContextKeys.HousemateId];

        PushSubscribeRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<PushSubscribeRequest>(ct);
        }
        catch
        {
            return new BadRequestObjectResult(new ApiErrorResponse("Invalid request body.", ApiErrorCodes.BadRequest));
        }

        if (body is null)
            return new BadRequestObjectResult(new ApiErrorResponse("Request body is required.", ApiErrorCodes.BadRequest));

        if (string.IsNullOrWhiteSpace(body.Endpoint))
            return new UnprocessableEntityObjectResult(new ApiErrorResponse("Endpoint is required.", ApiErrorCodes.ValidationError));

        if (string.IsNullOrWhiteSpace(body.P256dhKey))
            return new UnprocessableEntityObjectResult(new ApiErrorResponse("P256dhKey is required.", ApiErrorCodes.ValidationError));

        if (string.IsNullOrWhiteSpace(body.AuthKey))
            return new UnprocessableEntityObjectResult(new ApiErrorResponse("AuthKey is required.", ApiErrorCodes.ValidationError));

        await _pushHandler.SubscribeAsync(householdId, housemateId, body.Endpoint, body.P256dhKey, body.AuthKey, body.Locale, ct);

        return new NoContentResult();
    }

    /// <summary>Sends a nudge push notification to selected housemates for the given date.</summary>
    [Function("PostNudge")]
    public async Task<IActionResult> NudgeAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "days/{date}/nudge")] HttpRequest req,
        string date,
        FunctionContext context,
        CancellationToken ct)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];
        var senderHousemateId = (Guid)context.Items[FunctionContextKeys.HousemateId];

        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
            return new BadRequestObjectResult(new ApiErrorResponse("Date must be in yyyy-MM-dd format.", ApiErrorCodes.BadRequest));

        NudgeRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<NudgeRequest>(ct);
        }
        catch
        {
            return new BadRequestObjectResult(new ApiErrorResponse("Invalid request body.", ApiErrorCodes.BadRequest));
        }

        if (body is null)
            return new BadRequestObjectResult(new ApiErrorResponse("Request body is required.", ApiErrorCodes.BadRequest));

        if (body.RecipientHousemateIds is null || body.RecipientHousemateIds.Count == 0)
            return new UnprocessableEntityObjectResult(new ApiErrorResponse("At least one recipient is required.", ApiErrorCodes.ValidationError));

        var result = await _pushHandler.NudgeAsync(
            householdId,
            senderHousemateId,
            parsedDate,
            body.RecipientHousemateIds,
            body.PredefinedMessageKey,
            body.Message,
            ct);

        if (result is null)
            return new UnprocessableEntityObjectResult(new ApiErrorResponse(
                "Invalid nudge request: provide either a predefined message key or a custom message (max 20 chars), and ensure all recipients have Unknown status.",
                ApiErrorCodes.ValidationError));

        return new OkObjectResult(new NudgeResponse(result.Failures));
    }
}
