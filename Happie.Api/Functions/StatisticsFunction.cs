using Happie.Api.Constants;
using Happie.Api.Handlers;
using Happie.Api.Http;
using Happie.Api.Infrastructure.Repositories;
using Happie.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Happie.Api.Functions;

/// <summary>Azure Function that handles statistics requests for dishes and housemates.</summary>
public class StatisticsFunction
{
    private readonly IDishStatisticsHandler _dishStatisticsHandler;
    private readonly IHousemateStatisticsHandler _housemateStatisticsHandler;
    private readonly ISavedDishRepository _savedDishRepository;
    private readonly IHousemateRepository _housemateRepository;

    /// <summary>Initializes a new instance of <see cref="StatisticsFunction"/>.</summary>
    public StatisticsFunction(
        IDishStatisticsHandler dishStatisticsHandler,
        IHousemateStatisticsHandler housemateStatisticsHandler,
        ISavedDishRepository savedDishRepository,
        IHousemateRepository housemateRepository)
    {
        _dishStatisticsHandler = dishStatisticsHandler;
        _housemateStatisticsHandler = housemateStatisticsHandler;
        _savedDishRepository = savedDishRepository;
        _housemateRepository = housemateRepository;
    }

    /// <summary>Returns statistics for a saved dish within the authenticated household.</summary>
    [Function("GetDishStatistics")]
    public async Task<IActionResult> GetDishStatisticsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "saved-dishes/{id}/statistics")] HttpRequest request,
        string id,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        if (!RouteParser.TryParseGuid(id, out var savedDishId, out var guidError))
            return guidError;

        if (!TryParseDateRange(request, out var from, out var to, out var dateError))
            return dateError;

        var savedDish = await _savedDishRepository.GetAsync(householdId, savedDishId, cancellationToken);
        if (savedDish is null || savedDish.IsDeleted)
            return new NotFoundObjectResult(new ApiErrorResponse("Saved dish not found.", ApiErrorCodes.NotFound));

        var result = await _dishStatisticsHandler.GetStatisticsAsync(householdId, savedDishId, from, to, cancellationToken);

        var response = new DishStatisticsResponse(
            result.TimesCooked,
            result.AllTimeTimesCooked,
            result.LastCookedDate?.ToString("yyyy-MM-dd"),
            result.FirstCookedDate?.ToString("yyyy-MM-dd"),
            result.CookingShares.Select(x => new CookingShareDto(
                x.HousemateId,
                x.HousemateName,
                x.HousemateColor,
                x.ChefDayCount)).ToList());

        return new OkObjectResult(response);
    }

    /// <summary>Returns timeline data for a saved dish within the authenticated household.</summary>
    [Function("GetDishTimeline")]
    public async Task<IActionResult> GetDishTimelineAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "saved-dishes/{id}/timeline")] HttpRequest request,
        string id,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        if (!RouteParser.TryParseGuid(id, out var savedDishId, out var guidError))
            return guidError;

        if (!TryParseTimelineRange(request, out var timelineFrom, out var timelineTo, out var dateError))
            return dateError;

        var savedDish = await _savedDishRepository.GetAsync(householdId, savedDishId, cancellationToken);
        if (savedDish is null || savedDish.IsDeleted)
            return new NotFoundObjectResult(new ApiErrorResponse("Saved dish not found.", ApiErrorCodes.NotFound));

        var entries = await _dishStatisticsHandler.GetTimelineAsync(householdId, savedDishId, timelineFrom, timelineTo, cancellationToken);

        var response = new DishTimelineResponse(
            entries.Entries.Select(x => new DishTimelineDto(
                x.HousemateId,
                x.HousemateName,
                x.HousemateColor,
                x.SortOrder,
                x.CookingDays.Select(d => d.ToString("yyyy-MM-dd")).ToList())).ToList(),
            entries.FirstCookedDate?.ToString("yyyy-MM-dd"));

        return new OkObjectResult(response);
    }

    /// <summary>Returns statistics for a housemate within the authenticated household.</summary>
    [Function("GetHousemateStatistics")]
    public async Task<IActionResult> GetHousemateStatisticsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "housemates/{id}/statistics")] HttpRequest request,
        string id,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        if (!RouteParser.TryParseGuid(id, out var housemateId, out var guidError))
            return guidError;

        if (!TryParseDateRange(request, out var from, out var to, out var dateError))
            return dateError;

        var housemate = await _housemateRepository.GetAsync(householdId, housemateId, cancellationToken);
        if (housemate is null)
            return new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound));

        var result = await _housemateStatisticsHandler.GetStatisticsAsync(householdId, housemateId, from, to, cancellationToken);

        var response = new HousemateStatisticsResponse(
            result.TimesCooked,
            result.AllTimeTimesCooked,
            result.DaysEatingIn,
            result.CookRatioDays,
            result.CookRatioEatingInDays,
            result.LongestStreak,
            result.BusiestWeek,
            result.CookingShares.Select(x => new CookingShareDto(
                x.HousemateId,
                x.HousemateName,
                x.HousemateColor,
                x.ChefDayCount)).ToList(),
            result.TopDishes.Select(x => new TopDishDto(
                x.SavedDishId,
                x.Description,
                x.Count)).ToList());

        return new OkObjectResult(response);
    }

    /// <summary>Returns timeline data for a housemate within the authenticated household.</summary>
    [Function("GetHousemateTimeline")]
    public async Task<IActionResult> GetHousemateTimelineAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "housemates/{id}/timeline")] HttpRequest request,
        string id,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];

        if (!RouteParser.TryParseGuid(id, out var housemateId, out var guidError))
            return guidError;

        if (!TryParseTimelineRange(request, out var timelineFrom, out var timelineTo, out var dateError))
            return dateError;

        var housemate = await _housemateRepository.GetAsync(householdId, housemateId, cancellationToken);
        if (housemate is null)
            return new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound));

        var entries = await _housemateStatisticsHandler.GetTimelineAsync(householdId, housemateId, timelineFrom, timelineTo, cancellationToken);

        var response = new HousemateTimelineResponse(
            entries.Entries.Select(x => new HousemateTimelineDto(
                x.SavedDishId,
                x.DishDescription,
                x.AllTimeFrequency,
                x.CookingDays.Select(d => d.ToString("yyyy-MM-dd")).ToList())).ToList(),
            entries.FirstCookedDate?.ToString("yyyy-MM-dd"));

        return new OkObjectResult(response);
    }

    /// <summary>Parses the from and to query parameters for statistics endpoints.</summary>
    private static bool TryParseDateRange(
        HttpRequest request,
        out DateOnly from,
        out DateOnly to,
        out IActionResult error)
    {
        from = default;
        to = default;
        error = null!;

        var fromString = request.Query["from"].ToString();
        var toString = request.Query["to"].ToString();

        if (string.IsNullOrEmpty(fromString) || string.IsNullOrEmpty(toString))
        {
            error = new BadRequestObjectResult(new ApiErrorResponse("Date parameters (from, to) are required.", ApiErrorCodes.BadRequest));
            return false;
        }

        if (!RouteParser.TryParseDate(fromString, out from, out var fromError))
        {
            error = fromError;
            return false;
        }

        if (!RouteParser.TryParseDate(toString, out to, out var toError))
        {
            error = toError;
            return false;
        }

        if (from > to)
        {
            error = new BadRequestObjectResult(new ApiErrorResponse("'from' date must not be after 'to' date.", ApiErrorCodes.BadRequest));
            return false;
        }

        return true;
    }

    /// <summary>Parses the from and to query parameters for timeline endpoints.</summary>
    private static bool TryParseTimelineRange(
        HttpRequest request,
        out DateOnly from,
        out DateOnly to,
        out IActionResult error)
    {
        from = default;
        to = default;
        error = null!;

        var fromString = request.Query["from"].ToString();
        var toString = request.Query["to"].ToString();

        if (string.IsNullOrEmpty(fromString) || string.IsNullOrEmpty(toString))
        {
            error = new BadRequestObjectResult(new ApiErrorResponse("Date parameters (from, to) are required.", ApiErrorCodes.BadRequest));
            return false;
        }

        if (!RouteParser.TryParseDate(fromString, out from, out var fromError))
        {
            error = fromError;
            return false;
        }

        if (!RouteParser.TryParseDate(toString, out to, out var toError))
        {
            error = toError;
            return false;
        }

        if (from > to)
        {
            error = new BadRequestObjectResult(new ApiErrorResponse("'from' date must not be after 'to' date.", ApiErrorCodes.BadRequest));
            return false;
        }

        return true;
    }
}
