using Happie.Api.Constants;
using Happie.Api.Handlers;
using Happie.Api.Http;
using Happie.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Happie.Api.Functions;

/// <summary>Azure Function that handles day plan requests.</summary>
public class DaysFunction
{
    private readonly IDayHandler _dayHandler;

    /// <summary>Initializes a new instance of <see cref="DaysFunction"/>.</summary>
    public DaysFunction(IDayHandler dayHandler)
    {
        _dayHandler = dayHandler;
    }

    /// <summary>Returns the full day plan for the given date.</summary>
    [Function("GetDayPlan")]
    public async Task<IActionResult> GetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "days/{date}")] HttpRequest request,
        string date,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        if (!RouteParser.TryParseDate(date, out var parsedDate, out var error))
            return error;

        var dayPlan = await _dayHandler.GetDayPlanAsync(householdId, parsedDate, cancellationToken);

        return new OkObjectResult(dayPlan);
    }

    /// <summary>Returns attendance summaries for a date range, used by the calendar view.</summary>
    [Function("GetCalendar")]
    public async Task<IActionResult> GetCalendarAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "days")] HttpRequest request,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        var fromString = request.Query["from"].FirstOrDefault() ?? string.Empty;
        var toString = request.Query["to"].FirstOrDefault() ?? string.Empty;

        if (!RouteParser.TryParseDate(fromString, out var from, out _))
            return new BadRequestObjectResult(new ApiErrorResponse("Query parameter 'from' must be in yyyy-MM-dd format.", ApiErrorCodes.BadRequest));

        if (!RouteParser.TryParseDate(toString, out var to, out _))
            return new BadRequestObjectResult(new ApiErrorResponse("Query parameter 'to' must be in yyyy-MM-dd format.", ApiErrorCodes.BadRequest));

        if (to < from)
            return new BadRequestObjectResult(new ApiErrorResponse("'to' must be on or after 'from'.", ApiErrorCodes.BadRequest));

        var calendar = await _dayHandler.GetCalendarAsync(householdId, from, to, cancellationToken);

        return new OkObjectResult(calendar);
    }

    /// <summary>Upserts the attendance status for a housemate on the given date.</summary>
    [Function("PutAttendance")]
    public async Task<IActionResult> PutAttendanceAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "days/{date}/attendance/{housemateId}")] HttpRequest request,
        string date,
        string housemateId,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];
        var actingHousemateId = (Guid)context.Items[FunctionContextKeys.HousemateId];

        if (!RouteParser.TryParseDate(date, out var parsedDate, out var dateError))
            return dateError;

        if (!RouteParser.TryParseGuid(housemateId, out var parsedHousemateId, out var guidError))
            return guidError;

        var readResult = await RequestValidator.ReadAndValidateAsync<UpdateAttendanceRequest>(request, cancellationToken);
        if (!readResult.IsSuccess)
            return readResult.Error;

        var found = await _dayHandler.UpsertAttendanceAsync(householdId, parsedDate, parsedHousemateId, readResult.Body.Status, actingHousemateId, cancellationToken);

        if (!found)
            return new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound));

        return new NoContentResult();
    }

    /// <summary>Upserts the dish description for the given date.</summary>
    [Function("PutDish")]
    public async Task<IActionResult> PutDishAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "days/{date}/dish")] HttpRequest request,
        string date,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];
        var actingHousemateId = (Guid)context.Items[FunctionContextKeys.HousemateId];

        if (!RouteParser.TryParseDate(date, out var parsedDate, out var error))
            return error;

        var readResult = await RequestValidator.ReadAndValidateAsync<UpdateDishRequest>(request, cancellationToken);
        if (!readResult.IsSuccess)
            return readResult.Error;

        await _dayHandler.UpsertDishAsync(householdId, parsedDate, readResult.Body.Description.Trim(), actingHousemateId, cancellationToken);

        return new NoContentResult();
    }

    /// <summary>Upserts the comment for a housemate on the given date.</summary>
    [Function("PutComment")]
    public async Task<IActionResult> PutCommentAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "days/{date}/comments/{housemateId}")] HttpRequest request,
        string date,
        string housemateId,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];
        var actingHousemateId = (Guid)context.Items[FunctionContextKeys.HousemateId];

        if (!RouteParser.TryParseDate(date, out var parsedDate, out var dateError))
            return dateError;

        if (!RouteParser.TryParseGuid(housemateId, out var parsedHousemateId, out var guidError))
            return guidError;

        var readResult = await RequestValidator.ReadAndValidateAsync<UpdateCommentRequest>(request, cancellationToken);
        if (!readResult.IsSuccess)
            return readResult.Error;

        var found = await _dayHandler.UpsertCommentAsync(householdId, parsedDate, parsedHousemateId, readResult.Body.Text.Trim(), actingHousemateId, cancellationToken);

        if (!found)
            return new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound));

        return new NoContentResult();
    }

    /// <summary>Upserts the chef status for a housemate on the given date.</summary>
    [Function("PutChefStatus")]
    public async Task<IActionResult> PutChefStatusAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "days/{date}/chef/{housemateId}")] HttpRequest request,
        string date,
        string housemateId,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];
        var actingHousemateId = (Guid)context.Items[FunctionContextKeys.HousemateId];

        if (!RouteParser.TryParseDate(date, out var parsedDate, out var dateError))
            return dateError;

        if (!RouteParser.TryParseGuid(housemateId, out var parsedHousemateId, out var guidError))
            return guidError;

        var readResult = await RequestValidator.ReadAndValidateAsync<UpdateChefStatusRequest>(request, cancellationToken);
        if (!readResult.IsSuccess)
            return readResult.Error;

        var found = await _dayHandler.UpsertChefStatusAsync(householdId, parsedDate, parsedHousemateId, readResult.Body.IsChef, actingHousemateId, cancellationToken);

        if (!found)
            return new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound));

        return new NoContentResult();
    }

    /// <summary>Deletes the comment for a housemate on the given date.</summary>
    [Function("DeleteComment")]
    public async Task<IActionResult> DeleteCommentAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "days/{date}/comments/{housemateId}")] HttpRequest request,
        string date,
        string housemateId,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];
        var actingHousemateId = (Guid)context.Items[FunctionContextKeys.HousemateId];

        if (!RouteParser.TryParseDate(date, out var parsedDate, out var dateError))
            return dateError;

        if (!RouteParser.TryParseGuid(housemateId, out var parsedHousemateId, out var guidError))
            return guidError;

        var found = await _dayHandler.DeleteCommentAsync(householdId, parsedDate, parsedHousemateId, actingHousemateId, cancellationToken);

        if (!found)
            return new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound));

        return new NoContentResult();
    }
}
