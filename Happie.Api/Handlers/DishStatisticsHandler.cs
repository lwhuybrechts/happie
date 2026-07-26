using Happie.Api.Domain;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Results;

namespace Happie.Api.Handlers;

/// <summary>Computes dish statistics from attendance and dish link data.</summary>
public class DishStatisticsHandler : IDishStatisticsHandler
{
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly IDayPlanDishLinkRepository _dayPlanDishLinkRepository;
    private readonly ISavedDishRepository _savedDishRepository;
    private readonly IHousemateRepository _housemateRepository;

    /// <summary>Initializes a new instance of <see cref="DishStatisticsHandler"/>.</summary>
    public DishStatisticsHandler(
        IAttendanceRepository attendanceRepository,
        IDayPlanDishLinkRepository dayPlanDishLinkRepository,
        ISavedDishRepository savedDishRepository,
        IHousemateRepository housemateRepository)
    {
        _attendanceRepository = attendanceRepository;
        _dayPlanDishLinkRepository = dayPlanDishLinkRepository;
        _savedDishRepository = savedDishRepository;
        _housemateRepository = housemateRepository;
    }

    /// <inheritdoc/>
    public async Task<DishStatisticsResult> GetStatisticsAsync(
        Guid householdId,
        Guid savedDishId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        // Load all data in parallel.
        var linksTask = _dayPlanDishLinkRepository.GetAllByHouseholdAsync(householdId, cancellationToken);
        var savedDishesTask = _savedDishRepository.GetAllAsync(householdId, cancellationToken);
        var attendanceTask = _attendanceRepository.GetAllByHouseholdAsync(householdId, cancellationToken);
        var housematesTask = _housemateRepository.GetAllAsync(householdId, cancellationToken);

        await Task.WhenAll(linksTask, savedDishesTask, attendanceTask, housematesTask);

        var allLinks = await linksTask;
        var allSavedDishes = await savedDishesTask;
        var allAttendance = await attendanceTask;
        var allHousemates = await housematesTask;

        // Build a set of non-deleted saved dish IDs for filtering.
        var nonDeletedDishIds = allSavedDishes
            .Where(x => !x.IsDeleted)
            .Select(x => x.Id)
            .ToHashSet();

        // Filter links to only those for the target dish where the dish is not deleted.
        var dishLinks = allLinks
            .Where(x => x.SavedDishId == savedDishId && nonDeletedDishIds.Contains(x.SavedDishId))
            .ToList();

        // All cooking days for this dish (distinct dates).
        var allCookingDays = dishLinks
            .Select(x => x.Date)
            .Distinct()
            .ToList();

        // Cooking days within the selected range.
        var cookingDaysInRange = allCookingDays
            .Where(x => x >= from && x <= to)
            .ToList();

        // Times cooked within the selected range.
        var timesCooked = cookingDaysInRange.Count;

        // All-time times cooked.
        var allTimeTimesCooked = allCookingDays.Count;

        // Last cooked date (maximum date across all cooking days).
        DateOnly? lastCookedDate = allCookingDays.Count > 0
            ? allCookingDays.Max()
            : null;

        // First cooked date (all-time, minimum date across all cooking days).
        DateOnly? firstCookedDate = allCookingDays.Count > 0
            ? allCookingDays.Min()
            : null;

        // Cooking shares: count chef days per non-deleted housemate on this dish's cooking days within [from, to].
        var cookingDaysInRangeSet = cookingDaysInRange.ToHashSet();
        var nonDeletedHousemates = allHousemates
            .Where(x => !x.IsDeleted)
            .ToList();

        var chefDaysByHousemate = allAttendance
            .Where(x => x.IsChef && cookingDaysInRangeSet.Contains(x.Date))
            .Where(x => nonDeletedHousemates.Any(h => h.Id == x.HousemateId))
            .GroupBy(x => x.HousemateId)
            .ToDictionary(x => x.Key, x => x.Select(r => r.Date).Distinct().Count());

        var cookingShares = nonDeletedHousemates
            .Select(x => new CookingShareEntry(
                x.Id,
                x.Name,
                x.Color,
                chefDaysByHousemate.TryGetValue(x.Id, out var count) ? count : 0))
            .ToList();

        return new DishStatisticsResult(
            timesCooked,
            allTimeTimesCooked,
            lastCookedDate,
            firstCookedDate,
            cookingShares);
    }

    /// <inheritdoc/>
    public async Task<DishTimelineResult> GetTimelineAsync(
        Guid householdId,
        Guid savedDishId,
        DateOnly timelineFrom,
        DateOnly timelineTo,
        CancellationToken cancellationToken = default)
    {
        // Load all data in parallel.
        var linksTask = _dayPlanDishLinkRepository.GetAllByHouseholdAsync(householdId, cancellationToken);
        var savedDishesTask = _savedDishRepository.GetAllAsync(householdId, cancellationToken);
        var housematesTask = _housemateRepository.GetAllAsync(householdId, cancellationToken);
        var attendanceTask = _attendanceRepository.GetAllByHouseholdAsync(householdId, cancellationToken);

        await Task.WhenAll(linksTask, savedDishesTask, housematesTask, attendanceTask);

        var allLinks = await linksTask;
        var allSavedDishes = await savedDishesTask;
        var allHousemates = await housematesTask;
        var allAttendance = await attendanceTask;

        // Build a set of non-deleted saved dish IDs for filtering.
        var nonDeletedDishIds = allSavedDishes
            .Where(x => !x.IsDeleted)
            .Select(x => x.Id)
            .ToHashSet();

        // If the target dish is deleted, return no timeline entries.
        if (!nonDeletedDishIds.Contains(savedDishId))
            return new DishTimelineResult([], null);

        // Build lookup of non-deleted housemates.
        var housemateById = allHousemates
            .Where(x => !x.IsDeleted)
            .ToDictionary(x => x.Id);

        // Filter links to only those for the target dish.
        var dishLinks = allLinks
            .Where(x => x.SavedDishId == savedDishId)
            .ToList();

        // Compute the earliest date this dish was ever cooked (all-time).
        var allDishDates = dishLinks.Select(x => x.Date).Distinct().ToList();
        DateOnly? firstCookedDate = allDishDates.Count > 0 ? allDishDates.Min() : null;

        // All cooking days for this dish within the timeline window.
        var cookingDaysInTimeline = allDishDates
            .Where(x => x >= timelineFrom && x <= timelineTo)
            .ToHashSet();

        // Get attendance records where IsChef=true within the timeline window on cooking days.
        var chefRecordsInTimeline = allAttendance
            .Where(x => x.IsChef && x.Date >= timelineFrom && x.Date <= timelineTo && cookingDaysInTimeline.Contains(x.Date))
            .ToList();

        // Group by housemate.
        var chefDaysByHousemate = chefRecordsInTimeline
            .GroupBy(x => x.HousemateId)
            .ToDictionary(x => x.Key, x => x.Select(r => r.Date).Distinct().OrderBy(d => d).ToList());

        // Build timeline entries sorted by housemate SortOrder.
        // Always include all non-deleted housemates so the chart renders row labels
        // even when no cooking days exist in the timeline window (allowing scroll-back).
        var entries = housemateById.Values
            .Select(x => new DishTimelineEntry(
                x.Id,
                x.Name,
                x.Color,
                x.SortOrder,
                chefDaysByHousemate.TryGetValue(x.Id, out var days)
                    ? days.AsReadOnly()
                    : new List<DateOnly>().AsReadOnly()))
            .OrderBy(x => x.SortOrder)
            .ToList();

        return new DishTimelineResult(entries, firstCookedDate);
    }
}
