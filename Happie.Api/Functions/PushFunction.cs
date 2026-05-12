using Happie.Api.Constants;
using Happie.Api.Handlers;
using Happie.Api.Http;
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "push/subscribe")] HttpRequest request,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];
        var housemateId = (Guid)context.Items[FunctionContextKeys.HousemateId];

        var readResult = await RequestValidator.ReadAndValidateAsync<PushSubscribeRequest>(request, cancellationToken);
        if (!readResult.IsSuccess)
            return readResult.Error;

        await _pushHandler.SubscribeAsync(householdId, housemateId, readResult.Body.Endpoint, readResult.Body.P256dhKey, readResult.Body.AuthKey, readResult.Body.Locale, cancellationToken);

        return new NoContentResult();
    }

    /// <summary>Sends a nudge push notification to selected housemates for the given date.</summary>
    [Function("PostNudge")]
    public async Task<IActionResult> NudgeAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "days/{date}/nudge")] HttpRequest request,
        string date,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];
        var senderHousemateId = (Guid)context.Items[FunctionContextKeys.HousemateId];

        if (!RouteParser.TryParseDate(date, out var parsedDate, out var error))
            return error;

        var readResult = await RequestValidator.ReadAndValidateAsync<NudgeRequest>(request, cancellationToken);
        if (!readResult.IsSuccess)
            return readResult.Error;

        var nudgeResult = await _pushHandler.NudgeAsync(
            householdId,
            senderHousemateId,
            parsedDate,
            readResult.Body.RecipientHousemateIds,
            readResult.Body.PredefinedMessageKey,
            readResult.Body.Message,
            cancellationToken);

        if (nudgeResult is null)
            return new UnprocessableEntityObjectResult(new ApiErrorResponse(
                "Invalid nudge request: provide either a predefined message key or a custom message (max 20 chars), and ensure all recipients have Unknown status.",
                ApiErrorCodes.ValidationError));

        return new OkObjectResult(new NudgeResponse(nudgeResult.Failures));
    }
}
