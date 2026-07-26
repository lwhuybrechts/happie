using Happie.Api.Domain;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Results;
using Happie.Shared.Domain;

namespace Happie.Api.Handlers;

/// <summary>Computes housemate statistics from attendance and dish link data.</summary>
public class HousemateStatisticsHandler : IHousemateStatisticsHandler
{
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly IDayPlanDishLinkRepository _dayPlanDishLinkRepository;
    private readonly ISavedDishRepository _savedDishRepository;
    private readonly IHousemateRepository _housemateRepository;

    /// <summary>Initializes a new instance of <see cref="HousemateStatisticsHandler"/>.</summary>
    public HousemateStatisticsHandler(
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
    public async Task<HousemateStatisticsResult> GetStatisticsAsync(
        Guid householdId,
        Guid housemateId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        // Load all data in parallel.
        var attendanceTask = _attendanceRepository.GetAllByHouseholdAsync(householdId, cancellationToken);
        var linksTask = _dayPlanDishLinkRepository.GetAllByHouseholdAsync(householdId, cancellationToken);
        var savedDishesTask = _savedDishRepository.GetAllAsync(householdId, cancellationToken);
        var housematesTask = _housemateRepository.GetAllAsync(householdId, cancellationToken);

        await Task.WhenAll(attendanceTask, linksTask, savedDishesTask, housematesTask);

        var allAttendance = await attendanceTask;
        var allLinks = await linksTask;
        var allSavedDishes = await savedDishesTask;
        var allHousemates = await housematesTask;

        // Build a lookup of non-deleted saved dishes by ID.
        var savedDishById = allSavedDishes
            .Where(x => !x.IsDeleted)
            .ToDictionary(x => x.Id);

        // Filter attendance records for the target housemate.
        var housemateAttendance = allAttendance
            .Where(x => x.HousemateId == housemateId)
            .ToList();

        // Chef days for the target housemate (distinct dates with IsChef=true).
        var allChefDays = housemateAttendance
            .Where(x => x.IsChef)
            .Select(x => x.Date)
            .Distinct()
            .ToList();

        var chefDaysInRange = allChefDays
            .Where(x => x >= from && x <= to)
            .ToList();

        // Times cooked within the selected range.
        var timesCooked = chefDaysInRange.Count;

        // All-time times cooked.
        var allTimeTimesCooked = allChefDays.Count;

        // Days eating in within the selected range.
        var eatingInDays = housemateAttendance
            .Where(x => x.Status == AttendanceStatus.EatingIn && x.Date >= from && x.Date <= to)
            .Select(x => x.Date)
            .Distinct()
            .ToList();

        var daysEatingIn = eatingInDays.Count;

        // Cook ratio: X = days with IsChef AND EatingIn within range, Y = days with EatingIn within range.
        var eatingInDaysSet = eatingInDays.ToHashSet();
        var cookRatioDays = chefDaysInRange
            .Count(x => eatingInDaysSet.Contains(x));
        var cookRatioEatingInDays = daysEatingIn;

        // Longest streak: longest consecutive run of calendar days with IsChef=true within [from, to].
        var longestStreak = ComputeLongestStreak(chefDaysInRange);

        // Busiest week: max chef days in any Monday-to-Sunday ISO week within the range.
        var busiestWeek = ComputeBusiestWeek(chefDaysInRange);

        // Cooking shares: for each non-deleted housemate, count their chef days within [from, to].
        var cookingShares = ComputeCookingShares(allAttendance, allHousemates, from, to);

        // Top dishes: on the target housemate's chef days within [from, to], find all DayPlanDishLinks
        // (excluding soft-deleted dishes). Group by SavedDishId, count, sort by count desc then alphabetically.
        var topDishes = ComputeTopDishes(chefDaysInRange, allLinks, savedDishById);

        return new HousemateStatisticsResult(
            timesCooked,
            allTimeTimesCooked,
            daysEatingIn,
            cookRatioDays,
            cookRatioEatingInDays,
            longestStreak,
            busiestWeek,
            cookingShares,
            topDishes);
    }

    /// <inheritdoc/>
    public async Task<HousemateTimelineResult> GetTimelineAsync(
        Guid householdId,
        Guid housemateId,
        DateOnly timelineFrom,
        DateOnly timelineTo,
        CancellationToken cancellationToken = default)
    {
        // Load all data in parallel.
        var attendanceTask = _attendanceRepository.GetAllByHouseholdAsync(householdId, cancellationToken);
        var linksTask = _dayPlanDishLinkRepository.GetAllByHouseholdAsync(householdId, cancellationToken);
        var savedDishesTask = _savedDishRepository.GetAllAsync(householdId, cancellationToken);

        await Task.WhenAll(attendanceTask, linksTask, savedDishesTask);

        var allAttendance = await attendanceTask;
        var allLinks = await linksTask;
        var allSavedDishes = await savedDishesTask;

        // Build a lookup of non-deleted saved dishes by ID.
        var savedDishById = allSavedDishes
            .Where(x => !x.IsDeleted)
            .ToDictionary(x => x.Id);

        // Filter attendance records for the target housemate.
        var housemateAttendance = allAttendance
            .Where(x => x.HousemateId == housemateId)
            .ToList();

        // Chef days for the target housemate (distinct dates with IsChef=true).
        var allChefDays = housemateAttendance
            .Where(x => x.IsChef)
            .Select(x => x.Date)
            .Distinct()
            .ToList();

        // Compute the earliest date this housemate ever cooked.
        DateOnly? firstCookedDate = allChefDays.Count > 0 ? allChefDays.Min() : null;

        var entries = ComputeTimelineEntries(allChefDays, allLinks, savedDishById, timelineFrom, timelineTo);
        return new HousemateTimelineResult(entries, firstCookedDate);
    }

    /// <summary>Computes the longest consecutive run of calendar days in the given chef days.</summary>
    private static int ComputeLongestStreak(List<DateOnly> chefDaysInRange)
    {
        if (chefDaysInRange.Count == 0)
            return 0;

        var sortedDays = chefDaysInRange
            .OrderBy(x => x)
            .ToList();

        var longestStreak = 1;
        var currentStreak = 1;

        for (var i = 1; i < sortedDays.Count; i++)
        {
            if (sortedDays[i].DayNumber - sortedDays[i - 1].DayNumber == 1)
                currentStreak++;
            else
                currentStreak = 1;

            if (currentStreak > longestStreak)
                longestStreak = currentStreak;
        }

        return longestStreak;
    }

    /// <summary>Computes the maximum chef days in any Monday-to-Sunday ISO week.</summary>
    private static int ComputeBusiestWeek(List<DateOnly> chefDaysInRange)
    {
        if (chefDaysInRange.Count == 0)
            return 0;

        // Group chef days by ISO week (Monday-to-Sunday).
        var weekCounts = chefDaysInRange
            .GroupBy(x => GetIsoWeekStart(x))
            .Select(x => x.Count())
            .ToList();

        return weekCounts.Max();
    }

    /// <summary>Returns the Monday of the ISO week that contains the given date.</summary>
    private static DateOnly GetIsoWeekStart(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        // Convert Sunday=0 to 7 for ISO week calculation.
        var isoDayOfWeek = dayOfWeek == 0 ? 7 : dayOfWeek;
        return date.AddDays(1 - isoDayOfWeek);
    }

    /// <summary>Computes cooking shares for all non-deleted housemates within the date range.</summary>
    private static IReadOnlyList<CookingShareEntry> ComputeCookingShares(
        IReadOnlyList<AttendanceRecord> allAttendance,
        IReadOnlyList<Housemate> allHousemates,
        DateOnly from,
        DateOnly to)
    {
        // Build a lookup of non-deleted housemates.
        var nonDeletedHousemates = allHousemates
            .Where(x => !x.IsDeleted)
            .ToList();

        var nonDeletedHousemateIds = nonDeletedHousemates
            .Select(x => x.Id)
            .ToHashSet();

        // Count chef days per non-deleted housemate within the range.
        var chefDaysByHousemate = allAttendance
            .Where(x => x.IsChef && x.Date >= from && x.Date <= to && nonDeletedHousemateIds.Contains(x.HousemateId))
            .GroupBy(x => x.HousemateId)
            .ToDictionary(x => x.Key, x => x.Select(r => r.Date).Distinct().Count());

        return nonDeletedHousemates
            .Select(x => new CookingShareEntry(
                x.Id,
                x.Name,
                x.Color,
                chefDaysByHousemate.TryGetValue(x.Id, out var count) ? count : 0))
            .ToList();
    }

    /// <summary>Computes top dishes for the housemate's chef days within the range.</summary>
    private static IReadOnlyList<TopDishEntry> ComputeTopDishes(
        List<DateOnly> chefDaysInRange,
        IReadOnlyList<DayPlanDishLink> allLinks,
        Dictionary<Guid, SavedDish> savedDishById)
    {
        if (chefDaysInRange.Count == 0)
            return [];

        var chefDaysSet = chefDaysInRange.ToHashSet();

        // Find all DayPlanDishLinks on the housemate's chef days, excluding soft-deleted dishes.
        return allLinks
            .Where(x => chefDaysSet.Contains(x.Date) && savedDishById.ContainsKey(x.SavedDishId))
            .GroupBy(x => x.SavedDishId)
            .Select(x => new TopDishEntry(
                x.Key,
                savedDishById[x.Key].Description,
                x.Select(link => link.Date).Distinct().Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Description, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    /// <summary>Computes timeline entries for the housemate across all time with filtering to timeline window.</summary>
    private static IReadOnlyList<HousemateTimelineEntry> ComputeTimelineEntries(
        List<DateOnly> allChefDays,
        IReadOnlyList<DayPlanDishLink> allLinks,
        Dictionary<Guid, SavedDish> savedDishById,
        DateOnly timelineFrom,
        DateOnly timelineTo)
    {
        if (allChefDays.Count == 0)
            return [];

        var allChefDaysSet = allChefDays.ToHashSet();

        // Find all dish links on the housemate's chef days (non-deleted dishes only).
        var chefDishLinks = allLinks
            .Where(x => allChefDaysSet.Contains(x.Date) && savedDishById.ContainsKey(x.SavedDishId))
            .ToList();

        // Group by saved dish and compute all-time frequency (distinct dates).
        var dishGroups = chefDishLinks
            .GroupBy(x => x.SavedDishId)
            .Select(x => new
            {
                SavedDishId = x.Key,
                AllTimeDates = x.Select(link => link.Date).Distinct().ToList()
            })
            .Where(x => x.AllTimeDates.Count > 0)
            .ToList();

        // Build timeline entries with cooking days filtered to [timelineFrom, timelineTo].
        return dishGroups
            .Select(x => new HousemateTimelineEntry(
                x.SavedDishId,
                savedDishById[x.SavedDishId].Description,
                x.AllTimeDates.Count,
                x.AllTimeDates
                    .Where(d => d >= timelineFrom && d <= timelineTo)
                    .OrderBy(d => d)
                    .ToList()
                    .AsReadOnly()))
            .OrderByDescending(x => x.AllTimeFrequency)
            .ThenBy(x => x.DishDescription, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
