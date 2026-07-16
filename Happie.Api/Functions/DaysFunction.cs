using Happie.Api.Constants;
using Happie.Api.Handlers;
using Happie.Api.Http;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Results;
using Happie.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Happie.Api.Functions;

/// <summary>Azure Function that handles day plan requests.</summary>
public class DaysFunction
{
    private readonly IDayHandler _dayHandler;
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly IDishRepository _dishRepository;
    private readonly ICommentRepository _commentRepository;

    /// <summary>Initializes a new instance of <see cref="DaysFunction"/>.</summary>
    public DaysFunction(
        IDayHandler dayHandler,
        IAttendanceRepository attendanceRepository,
        IDishRepository dishRepository,
        ICommentRepository commentRepository)
    {
        _dayHandler = dayHandler;
        _attendanceRepository = attendanceRepository;
        _dishRepository = dishRepository;
        _commentRepository = commentRepository;
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

        var conflictResult = await CheckAttendanceConflictAsync(request, householdId, parsedDate, parsedHousemateId, cancellationToken);
        if (conflictResult is not null)
            return conflictResult;

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

        // Validate both-or-neither constraint on dinner time fields.
        if (readResult.Body.DinnerTimeHour.HasValue != readResult.Body.DinnerTimeMinute.HasValue)
            return new UnprocessableEntityObjectResult(new ApiErrorResponse("Both dinnerTimeHour and dinnerTimeMinute must be provided together or both must be null.", ApiErrorCodes.ValidationError));

        // Validate hour and minute ranges when provided.
        if (readResult.Body.DinnerTimeHour.HasValue)
        {
            if (readResult.Body.DinnerTimeHour.Value < 0 || readResult.Body.DinnerTimeHour.Value > 23)
                return new UnprocessableEntityObjectResult(new ApiErrorResponse("dinnerTimeHour must be between 0 and 23.", ApiErrorCodes.ValidationError));

            if (readResult.Body.DinnerTimeMinute!.Value < 0 || readResult.Body.DinnerTimeMinute.Value > 59)
                return new UnprocessableEntityObjectResult(new ApiErrorResponse("dinnerTimeMinute must be between 0 and 59.", ApiErrorCodes.ValidationError));
        }

        // Convert validated ints to TimeOnly?.
        TimeOnly? dinnerTime = readResult.Body.DinnerTimeHour.HasValue
            ? new TimeOnly(readResult.Body.DinnerTimeHour.Value, readResult.Body.DinnerTimeMinute!.Value)
            : null;

        var conflictResult = await CheckDishConflictAsync(request, householdId, parsedDate, cancellationToken);
        if (conflictResult is not null)
            return conflictResult;

        var description = readResult.Body.Description?.Trim();

        var result = await _dayHandler.UpsertDishAsync(householdId, parsedDate, description,
            dinnerTime, readResult.Body.TimezoneOffsetMinutes, actingHousemateId, cancellationToken);

        return result switch
        {
            DishUpsertResult.Success => new NoContentResult(),
            DishUpsertResult.Deleted => new NoContentResult(),
            DishUpsertResult.ValidationError => new UnprocessableEntityObjectResult(new ApiErrorResponse("Validation error.", ApiErrorCodes.ValidationError)),
            DishUpsertResult.SavedDishNotFound => new UnprocessableEntityObjectResult(new ApiErrorResponse("Referenced saved dish not found.", ApiErrorCodes.ValidationError)),
            _ => throw new InvalidOperationException($"Unhandled {nameof(DishUpsertResult)}: {result}"),
        };
    }

    /// <summary>Deletes the dish for the given date.</summary>
    [Function("DeleteDish")]
    public async Task<IActionResult> DeleteDishAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "days/{date}/dish")] HttpRequest request,
        string date,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];
        var actingHousemateId = (Guid)context.Items[FunctionContextKeys.HousemateId];

        if (!RouteParser.TryParseDate(date, out var parsedDate, out var error))
            return error;

        await _dayHandler.DeleteDishAsync(householdId, parsedDate, actingHousemateId, cancellationToken);

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

        var conflictResult = await CheckCommentConflictAsync(request, householdId, parsedDate, parsedHousemateId, cancellationToken);
        if (conflictResult is not null)
            return conflictResult;

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

        var conflictResult = await CheckCommentConflictAsync(request, householdId, parsedDate, parsedHousemateId, cancellationToken);
        if (conflictResult is not null)
            return conflictResult;

        var found = await _dayHandler.DeleteCommentAsync(householdId, parsedDate, parsedHousemateId, actingHousemateId, cancellationToken);

        if (!found)
            return new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound));

        return new NoContentResult();
    }

    /// <summary>Returns a 409 Conflict result if the attendance record has been modified after the If-Unmodified-Since header value.</summary>
    private async Task<IActionResult?> CheckAttendanceConflictAsync(HttpRequest request, Guid householdId, DateOnly date, Guid housemateId, CancellationToken cancellationToken)
    {
        if (!TryParseIfUnmodifiedSince(request, out var ifUnmodifiedSince))
            return null;

        var record = await _attendanceRepository.GetAsync(householdId, date, housemateId, cancellationToken);
        if (record?.LastModified is not null && record.LastModified.Value > ifUnmodifiedSince)
            return CreateConflictResult();

        return null;
    }

    /// <summary>Returns a 409 Conflict result if the dish record has been modified after the If-Unmodified-Since header value.</summary>
    private async Task<IActionResult?> CheckDishConflictAsync(HttpRequest request, Guid householdId, DateOnly date, CancellationToken cancellationToken)
    {
        if (!TryParseIfUnmodifiedSince(request, out var ifUnmodifiedSince))
            return null;

        var record = await _dishRepository.GetAsync(householdId, date, cancellationToken);
        if (record?.LastModified is not null && record.LastModified.Value > ifUnmodifiedSince)
            return CreateConflictResult();

        return null;
    }

    /// <summary>Returns a 409 Conflict result if the comment has been modified after the If-Unmodified-Since header value.</summary>
    private async Task<IActionResult?> CheckCommentConflictAsync(HttpRequest request, Guid householdId, DateOnly date, Guid housemateId, CancellationToken cancellationToken)
    {
        if (!TryParseIfUnmodifiedSince(request, out var ifUnmodifiedSince))
            return null;

        var comment = await _commentRepository.GetAsync(householdId, date, housemateId, cancellationToken);
        if (comment?.LastModified is not null && comment.LastModified.Value > ifUnmodifiedSince)
            return CreateConflictResult();

        return null;
    }

    /// <summary>Tries to parse the If-Unmodified-Since header value as a DateTimeOffset.</summary>
    private static bool TryParseIfUnmodifiedSince(HttpRequest request, out DateTimeOffset result)
    {
        var headerValue = request.Headers["If-Unmodified-Since"].FirstOrDefault();
        if (string.IsNullOrEmpty(headerValue))
        {
            result = default;
            return false;
        }

        return DateTimeOffset.TryParse(headerValue, out result);
    }

    /// <summary>Creates a 409 Conflict response.</summary>
    private static ObjectResult CreateConflictResult() =>
        new(new ApiErrorResponse("The resource has been modified since your offline change was made.", ApiErrorCodes.Conflict))
        {
            StatusCode = 409
        };
}
