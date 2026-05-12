using Happie.Api.Constants;
using Happie.Api.Handlers;
using Happie.Api.Models;
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "days/{date}")] HttpRequest req,
        string date,
        FunctionContext context,
        CancellationToken ct)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
            return new BadRequestObjectResult(new ApiErrorResponse("Date must be in yyyy-MM-dd format.", ApiErrorCodes.BadRequest));

        var dayPlan = await _dayHandler.GetDayPlanAsync(householdId, parsedDate, ct);

        return new OkObjectResult(dayPlan);
    }

    /// <summary>Returns attendance summaries for a date range, used by the calendar view.</summary>
    [Function("GetCalendar")]
    public async Task<IActionResult> GetCalendarAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "days")] HttpRequest req,
        FunctionContext context,
        CancellationToken ct)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        var fromStr = req.Query["from"].FirstOrDefault();
        var toStr = req.Query["to"].FirstOrDefault();

        if (fromStr is null || !DateOnly.TryParseExact(fromStr, "yyyy-MM-dd", out var from))
            return new BadRequestObjectResult(new ApiErrorResponse("Query parameter 'from' must be in yyyy-MM-dd format.", ApiErrorCodes.BadRequest));

        if (toStr is null || !DateOnly.TryParseExact(toStr, "yyyy-MM-dd", out var to))
            return new BadRequestObjectResult(new ApiErrorResponse("Query parameter 'to' must be in yyyy-MM-dd format.", ApiErrorCodes.BadRequest));

        if (to < from)
            return new BadRequestObjectResult(new ApiErrorResponse("'to' must be on or after 'from'.", ApiErrorCodes.BadRequest));

        var calendar = await _dayHandler.GetCalendarAsync(householdId, from, to, ct);

        return new OkObjectResult(calendar);
    }

    /// <summary>Upserts the attendance status for a housemate on the given date.</summary>
    [Function("PutAttendance")]
    public async Task<IActionResult> PutAttendanceAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "days/{date}/attendance/{housemateId}")] HttpRequest req,
        string date,
        string housemateId,
        FunctionContext context,
        CancellationToken ct)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];
        var actingHousemateId = (Guid)context.Items[FunctionContextKeys.HousemateId];

        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
            return new BadRequestObjectResult(new ApiErrorResponse("Date must be in yyyy-MM-dd format.", ApiErrorCodes.BadRequest));

        if (!Guid.TryParse(housemateId, out var parsedHousemateId))
            return new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound));

        UpdateAttendanceRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<UpdateAttendanceRequest>(ct);
        }
        catch
        {
            return new BadRequestObjectResult(new ApiErrorResponse("Invalid request body.", ApiErrorCodes.BadRequest));
        }

        if (body is null)
            return new BadRequestObjectResult(new ApiErrorResponse("Request body is required.", ApiErrorCodes.BadRequest));

        if (!Enum.IsDefined(body.Status))
            return new UnprocessableEntityObjectResult(new ApiErrorResponse("Invalid attendance status.", ApiErrorCodes.ValidationError));

        var found = await _dayHandler.UpsertAttendanceAsync(householdId, parsedDate, parsedHousemateId, body.Status, actingHousemateId, ct);

        if (!found)
            return new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound));

        return new NoContentResult();
    }

    /// <summary>Upserts the dish description for the given date.</summary>
    [Function("PutDish")]
    public async Task<IActionResult> PutDishAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "days/{date}/dish")] HttpRequest req,
        string date,
        FunctionContext context,
        CancellationToken ct)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];
        var actingHousemateId = (Guid)context.Items[FunctionContextKeys.HousemateId];

        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
            return new BadRequestObjectResult(new ApiErrorResponse("Date must be in yyyy-MM-dd format.", ApiErrorCodes.BadRequest));

        UpdateDishRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<UpdateDishRequest>(ct);
        }
        catch
        {
            return new BadRequestObjectResult(new ApiErrorResponse("Invalid request body.", ApiErrorCodes.BadRequest));
        }

        if (body is null)
            return new BadRequestObjectResult(new ApiErrorResponse("Request body is required.", ApiErrorCodes.BadRequest));

        var trimmed = body.Description.Trim();

        if (trimmed.Length > 100)
            return new UnprocessableEntityObjectResult(new ApiErrorResponse("Dish description must be at most 100 characters.", ApiErrorCodes.ValidationError));

        await _dayHandler.UpsertDishAsync(householdId, parsedDate, trimmed, actingHousemateId, ct);

        return new NoContentResult();
    }

    /// <summary>Upserts the comment for a housemate on the given date.</summary>
    [Function("PutComment")]
    public async Task<IActionResult> PutCommentAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "days/{date}/comments/{housemateId}")] HttpRequest req,
        string date,
        string housemateId,
        FunctionContext context,
        CancellationToken ct)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];
        var actingHousemateId = (Guid)context.Items[FunctionContextKeys.HousemateId];

        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
            return new BadRequestObjectResult(new ApiErrorResponse("Date must be in yyyy-MM-dd format.", ApiErrorCodes.BadRequest));

        if (!Guid.TryParse(housemateId, out var parsedHousemateId))
            return new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound));

        UpdateCommentRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<UpdateCommentRequest>(ct);
        }
        catch
        {
            return new BadRequestObjectResult(new ApiErrorResponse("Invalid request body.", ApiErrorCodes.BadRequest));
        }

        if (body is null)
            return new BadRequestObjectResult(new ApiErrorResponse("Request body is required.", ApiErrorCodes.BadRequest));

        var trimmed = body.Text.Trim();

        if (trimmed.Length > 200)
            return new UnprocessableEntityObjectResult(new ApiErrorResponse("Comment must be at most 200 characters.", ApiErrorCodes.ValidationError));

        var found = await _dayHandler.UpsertCommentAsync(householdId, parsedDate, parsedHousemateId, trimmed, actingHousemateId, ct);

        if (!found)
            return new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound));

        return new NoContentResult();
    }

    /// <summary>Deletes the comment for a housemate on the given date.</summary>
    [Function("DeleteComment")]
    public async Task<IActionResult> DeleteCommentAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "days/{date}/comments/{housemateId}")] HttpRequest req,
        string date,
        string housemateId,
        FunctionContext context,
        CancellationToken ct)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];
        var actingHousemateId = (Guid)context.Items[FunctionContextKeys.HousemateId];

        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
            return new BadRequestObjectResult(new ApiErrorResponse("Date must be in yyyy-MM-dd format.", ApiErrorCodes.BadRequest));

        if (!Guid.TryParse(housemateId, out var parsedHousemateId))
            return new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound));

        var found = await _dayHandler.DeleteCommentAsync(householdId, parsedDate, parsedHousemateId, actingHousemateId, ct);

        if (!found)
            return new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound));

        return new NoContentResult();
    }
}
