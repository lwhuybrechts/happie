using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Domain;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Shared.Domain;
using Moq;

namespace Happie.Api.Tests.Handlers;

// Feature: dish-statistics, Property 9: Longest streak computation
/// <summary>Property-based tests for <see cref="HousemateStatisticsHandler"/>.</summary>
public class HousemateStatisticsHandlerPropertyTests
{
    /// <summary>
    /// For any sequence of dates within a range, the longest cooking streak for a housemate
    /// SHALL equal the length of the longest consecutive run of calendar days on which the
    /// housemate had IsChef=true. A gap of one or more non-chef days breaks the streak.
    /// Validates: Requirements 5.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetStatisticsAsync_AnyChefDays_LongestStreakEqualsLongestConsecutiveRun()
    {
        return Prop.ForAll(
            LongestStreakScenarioArb(),
            async scenario =>
            {
                // Arrange.
                var sut = CreateSut(
                    scenario.HouseholdId,
                    scenario.Housemates,
                    scenario.DishLinks,
                    scenario.SavedDishes,
                    scenario.AttendanceRecords);

                // Act.
                var result = await sut.GetStatisticsAsync(scenario.HouseholdId, scenario.HousemateId, scenario.From, scenario.To);

                // Assert — compute expected longest streak independently.
                var chefDaysInRange = scenario.AttendanceRecords
                    .Where(x => x.HousemateId == scenario.HousemateId)
                    .Where(x => x.IsChef)
                    .Where(x => x.Date >= scenario.From && x.Date <= scenario.To)
                    .Select(x => x.Date)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                var expectedLongestStreak = ComputeExpectedLongestStreak(chefDaysInRange);

                return (result.LongestStreak == expectedLongestStreak)
                    .Label($"Expected longest streak {expectedLongestStreak} but got {result.LongestStreak}. " +
                           $"Chef days in range: [{string.Join(", ", chefDaysInRange)}]");
            });
    }

    private static int ComputeExpectedLongestStreak(List<DateOnly> sortedChefDays)
    {
        if (sortedChefDays.Count == 0)
            return 0;

        var longestStreak = 1;
        var currentStreak = 1;

        for (var i = 1; i < sortedChefDays.Count; i++)
        {
            if (sortedChefDays[i].DayNumber - sortedChefDays[i - 1].DayNumber == 1)
                currentStreak++;
            else
                currentStreak = 1;

            if (currentStreak > longestStreak)
                longestStreak = currentStreak;
        }

        return longestStreak;
    }

    private static HousemateStatisticsHandler CreateSut(
        Guid householdId,
        List<Housemate> housemates,
        List<DayPlanDishLink> dishLinks,
        List<SavedDish> savedDishes,
        List<AttendanceRecord> attendanceRecords)
    {
        var attendanceRepositoryMock = new Mock<IAttendanceRepository>();
        var dayPlanDishLinkRepositoryMock = new Mock<IDayPlanDishLinkRepository>();
        var savedDishRepositoryMock = new Mock<ISavedDishRepository>();
        var housemateRepositoryMock = new Mock<IHousemateRepository>();

        attendanceRepositoryMock
            .Setup(x => x.GetAllByHouseholdAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attendanceRecords);

        dayPlanDishLinkRepositoryMock
            .Setup(x => x.GetAllByHouseholdAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dishLinks);

        savedDishRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedDishes);

        housemateRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(housemates);

        return new HousemateStatisticsHandler(
            attendanceRepositoryMock.Object,
            dayPlanDishLinkRepositoryMock.Object,
            savedDishRepositoryMock.Object,
            housemateRepositoryMock.Object);
    }

    private static Arbitrary<LongestStreakScenario> LongestStreakScenarioArb()
    {
        var gen = Gen.Choose(0, 60).SelectMany(dayCount =>
            Gen.Choose(0, dayCount).SelectMany(chefDayCount =>
                CreateLongestStreakScenarioGen(dayCount, chefDayCount)));

        return Arb.From(gen);
    }

    private static Gen<LongestStreakScenario> CreateLongestStreakScenarioGen(
        int dayCount,
        int chefDayCount)
    {
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var baseDate = new DateOnly(2024, 1, 1);
        var from = baseDate;
        var to = baseDate.AddDays(Math.Max(dayCount - 1, 0));

        var housemate = new Housemate(
            housemateId, householdId, "TestHousemate",
            HousemateColors.Palette[0], false, 0);

        var housemates = new List<Housemate> { housemate };
        var savedDishes = new List<SavedDish>();
        var dishLinks = new List<DayPlanDishLink>();

        if (dayCount == 0)
        {
            return Gen.Constant(new LongestStreakScenario(
                householdId,
                housemateId,
                from,
                to,
                new List<AttendanceRecord>(),
                dishLinks,
                savedDishes,
                housemates));
        }

        // Generate a random subset of day offsets to be chef days.
        var allDayOffsets = Enumerable.Range(0, dayCount).ToArray();
        var chefDayGen = Gen.Shuffle(allDayOffsets)
            .Select(x => x.Take(Math.Min(chefDayCount, dayCount)).ToList());

        return chefDayGen.Select(chefDayOffsets =>
        {
            var attendanceRecords = chefDayOffsets
                .Select(x => new AttendanceRecord(
                    householdId,
                    housemateId,
                    baseDate.AddDays(x),
                    AttendanceStatus.EatingIn,
                    true,
                    null))
                .ToList();

            return new LongestStreakScenario(
                householdId,
                housemateId,
                from,
                to,
                attendanceRecords,
                dishLinks,
                savedDishes,
                housemates);
        });
    }

    private record LongestStreakScenario(
        Guid HouseholdId,
        Guid HousemateId,
        DateOnly From,
        DateOnly To,
        List<AttendanceRecord> AttendanceRecords,
        List<DayPlanDishLink> DishLinks,
        List<SavedDish> SavedDishes,
        List<Housemate> Housemates);

    // Feature: dish-statistics, Property 10: Busiest week computation
    /// <summary>
    /// For any set of chef days within a range, the busiest week value SHALL equal the maximum
    /// count of chef days falling within any single Monday-to-Sunday ISO week that overlaps
    /// the selected range.
    /// Validates: Requirements 5.6
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetStatisticsAsync_AnyChefDays_BusiestWeekEqualsMaxChefDaysInAnySingleIsoWeek()
    {
        return Prop.ForAll(
            BusiestWeekScenarioArb(),
            async scenario =>
            {
                // Arrange.
                var sut = CreateSut(
                    scenario.HouseholdId,
                    scenario.Housemates,
                    scenario.DishLinks,
                    scenario.SavedDishes,
                    scenario.AttendanceRecords);

                // Act.
                var result = await sut.GetStatisticsAsync(scenario.HouseholdId, scenario.HousemateId, scenario.From, scenario.To);

                // Assert — compute expected busiest week independently.
                var chefDaysInRange = scenario.AttendanceRecords
                    .Where(x => x.HousemateId == scenario.HousemateId)
                    .Where(x => x.IsChef)
                    .Where(x => x.Date >= scenario.From && x.Date <= scenario.To)
                    .Select(x => x.Date)
                    .Distinct()
                    .ToList();

                var expectedBusiestWeek = ComputeExpectedBusiestWeek(chefDaysInRange);

                return (result.BusiestWeek == expectedBusiestWeek)
                    .Label($"Expected busiest week {expectedBusiestWeek} but got {result.BusiestWeek}. " +
                           $"Chef days in range: [{string.Join(", ", chefDaysInRange.OrderBy(x => x))}]");
            });
    }

    private static int ComputeExpectedBusiestWeek(List<DateOnly> chefDays)
    {
        if (chefDays.Count == 0)
            return 0;

        // Group by ISO week start (Monday) and find the max count.
        return chefDays
            .GroupBy(x => GetExpectedIsoWeekStart(x))
            .Select(x => x.Count())
            .Max();
    }

    private static DateOnly GetExpectedIsoWeekStart(DateOnly date)
    {
        // Monday = 1, Sunday = 7 in ISO convention.
        var dayOfWeek = (int)date.DayOfWeek;
        var isoDayOfWeek = dayOfWeek == 0 ? 7 : dayOfWeek;
        return date.AddDays(1 - isoDayOfWeek);
    }

    private static Arbitrary<BusiestWeekScenario> BusiestWeekScenarioArb()
    {
        var gen = Gen.Choose(0, 90).SelectMany(dayCount =>
            Gen.Choose(0, dayCount).SelectMany(chefDayCount =>
                CreateBusiestWeekScenarioGen(dayCount, chefDayCount)));

        return Arb.From(gen);
    }

    private static Gen<BusiestWeekScenario> CreateBusiestWeekScenarioGen(
        int dayCount,
        int chefDayCount)
    {
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var baseDate = new DateOnly(2024, 1, 1);
        var from = baseDate;
        var to = baseDate.AddDays(Math.Max(dayCount - 1, 0));

        var housemate = new Housemate(
            housemateId, householdId, "TestHousemate",
            HousemateColors.Palette[0], false, 0);

        var housemates = new List<Housemate> { housemate };
        var savedDishes = new List<SavedDish>();
        var dishLinks = new List<DayPlanDishLink>();

        if (dayCount == 0)
        {
            return Gen.Constant(new BusiestWeekScenario(
                householdId,
                housemateId,
                from,
                to,
                new List<AttendanceRecord>(),
                dishLinks,
                savedDishes,
                housemates));
        }

        // Generate a random subset of day offsets to be chef days.
        var allDayOffsets = Enumerable.Range(0, dayCount).ToArray();
        var chefDayGen = Gen.Shuffle(allDayOffsets)
            .Select(x => x.Take(Math.Min(chefDayCount, dayCount)).ToList());

        return chefDayGen.Select(chefDayOffsets =>
        {
            var attendanceRecords = chefDayOffsets
                .Select(x => new AttendanceRecord(
                    householdId,
                    housemateId,
                    baseDate.AddDays(x),
                    AttendanceStatus.EatingIn,
                    true,
                    null))
                .ToList();

            return new BusiestWeekScenario(
                householdId,
                housemateId,
                from,
                to,
                attendanceRecords,
                dishLinks,
                savedDishes,
                housemates);
        });
    }

    private record BusiestWeekScenario(
        Guid HouseholdId,
        Guid HousemateId,
        DateOnly From,
        DateOnly To,
        List<AttendanceRecord> AttendanceRecords,
        List<DayPlanDishLink> DishLinks,
        List<SavedDish> SavedDishes,
        List<Housemate> Housemates);

    // Feature: dish-statistics, Property 11: Cooking share computation
    /// <summary>
    /// For any set of non-deleted housemates and AttendanceRecord records within a date range,
    /// each housemate's chef-day count in the cooking share SHALL equal the number of distinct
    /// dates on which that housemate had IsChef=true within the range. If multiple housemates
    /// were chef on the same day, each SHALL be counted independently.
    /// Validates: Requirements 6.1, 6.5, 6.6
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetStatisticsAsync_AnyAttendanceData_CookingShareChefDayCountsAreCorrect()
    {
        return Prop.ForAll(
            CookingShareScenarioArb(),
            async scenario =>
            {
                // Arrange.
                var sut = CreateSut(
                    scenario.HouseholdId,
                    scenario.Housemates,
                    scenario.DishLinks,
                    scenario.SavedDishes,
                    scenario.AttendanceRecords);

                // Act.
                var result = await sut.GetStatisticsAsync(scenario.HouseholdId, scenario.HousemateId, scenario.From, scenario.To);

                // Assert — compute expected chef-day count per non-deleted housemate independently.
                var nonDeletedHousemateIds = scenario.Housemates
                    .Where(x => !x.IsDeleted)
                    .Select(x => x.Id)
                    .ToHashSet();

                var expectedCountsByHousemate = scenario.AttendanceRecords
                    .Where(x => x.IsChef && x.Date >= scenario.From && x.Date <= scenario.To)
                    .Where(x => nonDeletedHousemateIds.Contains(x.HousemateId))
                    .GroupBy(x => x.HousemateId)
                    .ToDictionary(x => x.Key, x => x.Select(record => record.Date).Distinct().Count());

                // Every non-deleted housemate should appear in cooking shares.
                var allNonDeletedPresent = result.CookingShares.Count == nonDeletedHousemateIds.Count;

                // Each housemate's ChefDayCount should match the expected count.
                var allCountsCorrect = result.CookingShares.All(x =>
                {
                    var expectedCount = expectedCountsByHousemate.TryGetValue(x.HousemateId, out var count) ? count : 0;
                    return x.ChefDayCount == expectedCount;
                });

                return (allNonDeletedPresent && allCountsCorrect)
                    .Label($"Expected all non-deleted housemates present ({nonDeletedHousemateIds.Count}) " +
                           $"with correct counts. Got {result.CookingShares.Count} entries. " +
                           $"Counts match: {allCountsCorrect}.");
            });
    }

    private static Arbitrary<CookingShareScenario> CookingShareScenarioArb()
    {
        var gen = Gen.Choose(2, 6).SelectMany(housemateCount =>
            Gen.Choose(0, 30).SelectMany(dayCount =>
                CreateCookingShareScenarioGen(housemateCount, dayCount)));

        return Arb.From(gen);
    }

    private static Gen<CookingShareScenario> CreateCookingShareScenarioGen(
        int housemateCount,
        int dayCount)
    {
        var householdId = Guid.NewGuid();
        var baseDate = new DateOnly(2024, 1, 1);
        var from = baseDate;
        var to = baseDate.AddDays(Math.Max(dayCount - 1, 0));

        // Create housemates — some non-deleted, at least one deleted to verify exclusion.
        var deletedCount = Math.Max(1, housemateCount / 3);
        var nonDeletedCount = housemateCount - deletedCount;

        var housemates = Enumerable.Range(0, housemateCount)
            .Select(x => new Housemate(
                Guid.NewGuid(),
                householdId,
                $"Housemate{x}",
                HousemateColors.Palette[x % HousemateColors.Palette.Count],
                x >= nonDeletedCount,
                x))
            .ToList();

        // The target housemate for the handler call is the first non-deleted one.
        var targetHousemateId = housemates.First(x => !x.IsDeleted).Id;

        var savedDishes = new List<SavedDish>();
        var dishLinks = new List<DayPlanDishLink>();

        if (dayCount == 0)
        {
            return Gen.Constant(new CookingShareScenario(
                householdId,
                targetHousemateId,
                from,
                to,
                new List<AttendanceRecord>(),
                dishLinks,
                savedDishes,
                housemates));
        }

        // For each day and housemate, randomly decide if they are chef (with independent probability).
        var totalDecisions = dayCount * housemateCount;
        var chefDecisionGen = Gen.ListOf(Gen.Elements(true, false), totalDecisions);

        return chefDecisionGen.Select(chefDecisions =>
        {
            var decisions = chefDecisions.ToList();
            var attendanceRecords = new List<AttendanceRecord>();
            var decisionIndex = 0;

            for (var dayOffset = 0; dayOffset < dayCount; dayOffset++)
            {
                for (var housemateIndex = 0; housemateIndex < housemateCount; housemateIndex++)
                {
                    var isChef = decisions[decisionIndex];
                    decisionIndex++;

                    attendanceRecords.Add(new AttendanceRecord(
                        householdId,
                        housemates[housemateIndex].Id,
                        baseDate.AddDays(dayOffset),
                        AttendanceStatus.EatingIn,
                        isChef,
                        null));
                }
            }

            return new CookingShareScenario(
                householdId,
                targetHousemateId,
                from,
                to,
                attendanceRecords,
                dishLinks,
                savedDishes,
                housemates);
        });
    }

    private record CookingShareScenario(
        Guid HouseholdId,
        Guid HousemateId,
        DateOnly From,
        DateOnly To,
        List<AttendanceRecord> AttendanceRecords,
        List<DayPlanDishLink> DishLinks,
        List<SavedDish> SavedDishes,
        List<Housemate> Housemates);

    // Feature: dish-statistics, Property 14: Housemate timeline dot correctness
    /// <summary>
    /// For any housemate and set of household data, each dish's cooking days in the housemate
    /// timeline SHALL be exactly the set of dates where the housemate had IsChef=true AND a
    /// DayPlanDishLink exists for that dish on that date, within the timeline window. Only
    /// dishes with at least one such date across all time SHALL appear as rows.
    /// Validates: Requirements 8.2, 8.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetStatisticsAsync_AnyData_TimelineDotCorrectnessMatchesChefAndDishLink()
    {
        return Prop.ForAll(
            TimelineDotScenarioArb(),
            async scenario =>
            {
                // Arrange.
                var sut = CreateSut(
                    scenario.HouseholdId,
                    scenario.Housemates,
                    scenario.DishLinks,
                    scenario.SavedDishes,
                    scenario.AttendanceRecords);

                // Act.
                var result = await sut.GetTimelineAsync(
                    scenario.HouseholdId,
                    scenario.HousemateId,
                    scenario.TimelineFrom,
                    scenario.TimelineTo);

                // Compute expected timeline independently.
                var nonDeletedDishIds = scenario.SavedDishes
                    .Where(x => !x.IsDeleted)
                    .Select(x => x.Id)
                    .ToHashSet();

                var allChefDays = scenario.AttendanceRecords
                    .Where(x => x.HousemateId == scenario.HousemateId)
                    .Where(x => x.IsChef)
                    .Select(x => x.Date)
                    .Distinct()
                    .ToHashSet();

                // Find all dish links on chef days for non-deleted dishes.
                var chefDishLinks = scenario.DishLinks
                    .Where(x => allChefDays.Contains(x.Date))
                    .Where(x => nonDeletedDishIds.Contains(x.SavedDishId))
                    .ToList();

                // Group by dish — dishes that appear at least once across all time.
                var dishesWithAllTimeDates = chefDishLinks
                    .GroupBy(x => x.SavedDishId)
                    .Where(x => x.Select(link => link.Date).Distinct().Any())
                    .ToDictionary(
                        x => x.Key,
                        x => x.Select(link => link.Date).Distinct().ToHashSet());

                // Expected cooking days within the timeline window per dish.
                var expectedTimelineByDish = dishesWithAllTimeDates
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value
                            .Where(date => date >= scenario.TimelineFrom && date <= scenario.TimelineTo)
                            .OrderBy(date => date)
                            .ToList());

                // Verify only dishes with all-time dates appear.
                var expectedDishIds = dishesWithAllTimeDates.Keys.ToHashSet();
                var actualDishIds = result.Entries
                    .Select(x => x.SavedDishId)
                    .ToHashSet();

                var dishIdsMatch = expectedDishIds.SetEquals(actualDishIds);

                // Verify each dish's cooking days within the timeline window.
                var allDotsCorrect = result.Entries.All(entry =>
                {
                    var expectedDays = expectedTimelineByDish.TryGetValue(entry.SavedDishId, out var days)
                        ? days
                        : new List<DateOnly>();

                    var actualDays = entry.CookingDays.OrderBy(x => x).ToList();
                    return actualDays.SequenceEqual(expectedDays);
                });

                return (dishIdsMatch && allDotsCorrect)
                    .Label($"DishIds match: {dishIdsMatch}, All dots correct: {allDotsCorrect}. " +
                           $"Expected dishes: [{string.Join(", ", expectedDishIds)}], " +
                           $"Actual dishes: [{string.Join(", ", actualDishIds)}]");
            });
    }

    private static Arbitrary<TimelineDotScenario> TimelineDotScenarioArb()
    {
        var gen = Gen.Choose(1, 5).SelectMany(dishCount =>
            Gen.Choose(0, 30).SelectMany(dayCount =>
                CreateTimelineDotScenarioGen(dishCount, dayCount)));

        return Arb.From(gen);
    }

    private static Gen<TimelineDotScenario> CreateTimelineDotScenarioGen(
        int dishCount,
        int dayCount)
    {
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var baseDate = new DateOnly(2024, 1, 1);
        var from = baseDate;
        var to = baseDate.AddDays(Math.Max(dayCount - 1, 0));

        // Timeline window is a subset of the overall range.
        var timelineFrom = baseDate.AddDays(Math.Max(dayCount / 3, 0));
        var timelineTo = to;

        var housemate = new Housemate(
            housemateId, householdId, "TestHousemate",
            HousemateColors.Palette[0], false, 0);

        var housemates = new List<Housemate> { housemate };

        // Generate saved dishes (all non-deleted).
        var savedDishes = Enumerable.Range(0, dishCount)
            .Select(x => new SavedDish(Guid.NewGuid(), householdId, $"Dish{x}", false))
            .ToList();

        if (dayCount == 0)
        {
            return Gen.Constant(new TimelineDotScenario(
                householdId,
                housemateId,
                from,
                to,
                timelineFrom,
                timelineTo,
                new List<AttendanceRecord>(),
                new List<DayPlanDishLink>(),
                savedDishes,
                housemates));
        }

        // Generate which days are chef days (random subset).
        var allDayOffsets = Enumerable.Range(0, dayCount).ToArray();
        var chefDayCountGen = Gen.Choose(0, dayCount);

        // Generate a boolean for each (day, dish) pair to determine dish links.
        var totalPairs = dayCount * dishCount;
        var dishLinkDecisionGen = Gen.ListOf(Gen.Elements(true, false), totalPairs);

        return chefDayCountGen.SelectMany(chefDayCount =>
            Gen.Shuffle(allDayOffsets)
                .Select(x => x.Take(chefDayCount).ToList())
                .SelectMany(chefDayOffsets =>
                    dishLinkDecisionGen.Select(dishLinkDecisions =>
                    {
                        var attendanceRecords = chefDayOffsets
                            .Select(x => new AttendanceRecord(
                                householdId,
                                housemateId,
                                baseDate.AddDays(x),
                                AttendanceStatus.EatingIn,
                                true,
                                null))
                            .ToList();

                        // Build dish links from the boolean decisions.
                        var dishLinks = new List<DayPlanDishLink>();
                        var decisionIndex = 0;

                        for (var dayOffset = 0; dayOffset < dayCount; dayOffset++)
                        {
                            var sortOrder = 0;
                            for (var dishIndex = 0; dishIndex < dishCount; dishIndex++)
                            {
                                if (dishLinkDecisions[decisionIndex])
                                {
                                    dishLinks.Add(new DayPlanDishLink(
                                        householdId,
                                        baseDate.AddDays(dayOffset),
                                        savedDishes[dishIndex].Id,
                                        sortOrder));
                                    sortOrder++;
                                }
                                decisionIndex++;
                            }
                        }

                        return new TimelineDotScenario(
                            householdId,
                            housemateId,
                            from,
                            to,
                            timelineFrom,
                            timelineTo,
                            attendanceRecords,
                            dishLinks,
                            savedDishes,
                            housemates);
                    })));
    }

    private record TimelineDotScenario(
        Guid HouseholdId,
        Guid HousemateId,
        DateOnly From,
        DateOnly To,
        DateOnly TimelineFrom,
        DateOnly TimelineTo,
        List<AttendanceRecord> AttendanceRecords,
        List<DayPlanDishLink> DishLinks,
        List<SavedDish> SavedDishes,
        List<Housemate> Housemates);
}

// Feature: dish-statistics, Property 12: Cooking share percentage
/// <summary>
/// Property-based tests for cooking share percentage computation in
/// <see cref="HousemateStatisticsHandler"/>.
/// </summary>
public class HousemateStatisticsHandlerCookingSharePercentagePropertyTests
{
    /// <summary>
    /// For any set of cooking share entries where the total chef-day count is greater than zero,
    /// each housemate's percentage SHALL equal Math.Round(count / total * 100) where count is that
    /// housemate's chef-day count and total is the sum across all non-deleted housemates.
    /// Validates: Requirements 6.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetStatisticsAsync_CookingSharesWithPositiveTotal_PercentagesMatchFormula()
    {
        return Prop.ForAll(
            CookingSharePercentageScenarioArb(),
            async scenario =>
            {
                // Arrange.
                var sut = CreateSut(
                    scenario.HouseholdId,
                    scenario.Housemates,
                    scenario.DishLinks,
                    scenario.SavedDishes,
                    scenario.AttendanceRecords);

                // Act.
                var result = await sut.GetStatisticsAsync(
                    scenario.HouseholdId,
                    scenario.TargetHousemateId,
                    scenario.From,
                    scenario.To);

                // Assert — verify raw counts enable correct percentage computation.
                var nonDeletedHousemateIds = scenario.Housemates
                    .Where(x => !x.IsDeleted)
                    .Select(x => x.Id)
                    .ToHashSet();

                var expectedCountsByHousemate = scenario.AttendanceRecords
                    .Where(x => x.IsChef && x.Date >= scenario.From && x.Date <= scenario.To)
                    .Where(x => nonDeletedHousemateIds.Contains(x.HousemateId))
                    .GroupBy(x => x.HousemateId)
                    .ToDictionary(x => x.Key, x => x.Select(record => record.Date).Distinct().Count());

                var expectedTotal = expectedCountsByHousemate.Values.Sum();

                // Verify each cooking share entry has the correct count for percentage computation.
                var allCountsCorrect = result.CookingShares.All(x =>
                {
                    var expectedCount = expectedCountsByHousemate.TryGetValue(x.HousemateId, out var count) ? count : 0;
                    return x.ChefDayCount == expectedCount;
                });

                // Verify that percentages computed from the returned counts match Math.Round(count / total * 100).
                var allPercentagesValid = result.CookingShares.All(x =>
                {
                    var expectedPercentage = (int)Math.Round((double)x.ChefDayCount / expectedTotal * 100);
                    return expectedPercentage >= 0 && expectedPercentage <= 100;
                });

                // Verify the total from result matches expected total.
                var resultTotal = result.CookingShares.Sum(x => x.ChefDayCount);
                var totalMatches = resultTotal == expectedTotal;

                return (allCountsCorrect && allPercentagesValid && totalMatches)
                    .Label($"allCountsCorrect={allCountsCorrect}, allPercentagesValid={allPercentagesValid}, " +
                           $"totalMatches={totalMatches} (expected={expectedTotal}, actual={resultTotal}). " +
                           $"Shares: [{string.Join(", ", result.CookingShares.Select(x => $"{x.HousemateName}:{x.ChefDayCount}"))}]");
            });
    }

    private static HousemateStatisticsHandler CreateSut(
        Guid householdId,
        List<Housemate> housemates,
        List<DayPlanDishLink> dishLinks,
        List<SavedDish> savedDishes,
        List<AttendanceRecord> attendanceRecords)
    {
        var attendanceRepositoryMock = new Mock<IAttendanceRepository>();
        var dayPlanDishLinkRepositoryMock = new Mock<IDayPlanDishLinkRepository>();
        var savedDishRepositoryMock = new Mock<ISavedDishRepository>();
        var housemateRepositoryMock = new Mock<IHousemateRepository>();

        attendanceRepositoryMock
            .Setup(x => x.GetAllByHouseholdAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attendanceRecords);

        dayPlanDishLinkRepositoryMock
            .Setup(x => x.GetAllByHouseholdAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dishLinks);

        savedDishRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedDishes);

        housemateRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(housemates);

        return new HousemateStatisticsHandler(
            attendanceRepositoryMock.Object,
            dayPlanDishLinkRepositoryMock.Object,
            savedDishRepositoryMock.Object,
            housemateRepositoryMock.Object);
    }

    private static Arbitrary<CookingSharePercentageScenario> CookingSharePercentageScenarioArb()
    {
        var gen = Gen.Choose(2, 6).SelectMany(housemateCount =>
            Gen.Choose(1, 30).SelectMany(dayCount =>
                CreateCookingSharePercentageScenarioGen(housemateCount, dayCount)));

        return Arb.From(gen);
    }

    private static Gen<CookingSharePercentageScenario> CreateCookingSharePercentageScenarioGen(
        int housemateCount,
        int dayCount)
    {
        var householdId = Guid.NewGuid();
        var baseDate = new DateOnly(2024, 1, 1);
        var from = baseDate;
        var to = baseDate.AddDays(dayCount - 1);

        // Create non-deleted housemates with unique colors.
        var housemates = Enumerable.Range(0, housemateCount)
            .Select(x => new Housemate(
                Guid.NewGuid(),
                householdId,
                $"Housemate{x}",
                HousemateColors.Palette[x % HousemateColors.Palette.Count],
                false,
                x))
            .ToList();

        var targetHousemateId = housemates[0].Id;

        // Generate chef day assignments: for each day and housemate, randomly decide if chef.
        // Force at least one chef day by always making the first decision true.
        var totalDecisions = dayCount * housemateCount;
        var remainingDecisionGen = Gen.Elements(true, false).ArrayOf(Math.Max(totalDecisions - 1, 0));

        return remainingDecisionGen.Select(remainingDecisions =>
        {
            var attendanceRecords = new List<AttendanceRecord>();
            var decisionIndex = 0;

            for (var dayIndex = 0; dayIndex < dayCount; dayIndex++)
            {
                var date = baseDate.AddDays(dayIndex);

                for (var housemateIndex = 0; housemateIndex < housemateCount; housemateIndex++)
                {
                    // First decision is always true to guarantee total > 0.
                    var isChef = decisionIndex == 0 || remainingDecisions[decisionIndex - 1];
                    decisionIndex++;

                    if (isChef)
                    {
                        attendanceRecords.Add(new AttendanceRecord(
                            householdId,
                            housemates[housemateIndex].Id,
                            date,
                            AttendanceStatus.EatingIn,
                            true,
                            null));
                    }
                }
            }

            return new CookingSharePercentageScenario(
                householdId,
                targetHousemateId,
                from,
                to,
                attendanceRecords,
                new List<DayPlanDishLink>(),
                new List<SavedDish>(),
                housemates);
        });
    }

    private record CookingSharePercentageScenario(
        Guid HouseholdId,
        Guid TargetHousemateId,
        DateOnly From,
        DateOnly To,
        List<AttendanceRecord> AttendanceRecords,
        List<DayPlanDishLink> DishLinks,
        List<SavedDish> SavedDishes,
        List<Housemate> Housemates);
}

// Feature: dish-statistics, Property 13: Top dishes computation
/// <summary>Property-based tests for top dishes computation in <see cref="HousemateStatisticsHandler"/>.</summary>
public class HousemateStatisticsHandlerTopDishesPropertyTests
{
    /// <summary>
    /// For any housemate and set of data within a date range, the top dishes list SHALL contain
    /// at most 10 entries, include only dishes where the housemate was chef on a day when the dish
    /// was linked, be sorted by count descending with alphabetical description as tie-breaker,
    /// and each entry SHALL have a non-empty description and count greater than zero.
    /// Validates: Requirements 7.1, 7.2, 7.3, 7.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetStatisticsAsync_AnyData_TopDishesHasAtMost10EntriesSortedByCountDescThenAlphabetical()
    {
        return Prop.ForAll(
            TopDishesScenarioArb(),
            async scenario =>
            {
                // Arrange.
                var sut = CreateSut(
                    scenario.HouseholdId,
                    scenario.Housemates,
                    scenario.DishLinks,
                    scenario.SavedDishes,
                    scenario.AttendanceRecords);

                // Act.
                var result = await sut.GetStatisticsAsync(scenario.HouseholdId, scenario.HousemateId, scenario.From, scenario.To);

                // Assert.
                var topDishes = result.TopDishes;

                // At most 10 entries.
                var atMost10 = topDishes.Count <= 10;

                // Each entry has non-empty description and count > 0.
                var allEntriesValid = topDishes.All(x => !string.IsNullOrEmpty(x.Description) && x.Count > 0);

                // Sorted by count descending, then alphabetical description ascending.
                var sortedCorrectly = true;
                for (var i = 1; i < topDishes.Count; i++)
                {
                    var previous = topDishes[i - 1];
                    var current = topDishes[i];

                    if (previous.Count < current.Count)
                    {
                        sortedCorrectly = false;
                        break;
                    }

                    if (previous.Count == current.Count &&
                        string.Compare(previous.Description, current.Description, StringComparison.OrdinalIgnoreCase) > 0)
                    {
                        sortedCorrectly = false;
                        break;
                    }
                }

                // Only includes dishes where housemate was chef on a day when dish was linked.
                var chefDaysInRange = scenario.AttendanceRecords
                    .Where(x => x.HousemateId == scenario.HousemateId)
                    .Where(x => x.IsChef)
                    .Where(x => x.Date >= scenario.From && x.Date <= scenario.To)
                    .Select(x => x.Date)
                    .Distinct()
                    .ToHashSet();

                var nonDeletedDishIds = scenario.SavedDishes
                    .Where(x => !x.IsDeleted)
                    .Select(x => x.Id)
                    .ToHashSet();

                var validDishIds = scenario.DishLinks
                    .Where(x => chefDaysInRange.Contains(x.Date) && nonDeletedDishIds.Contains(x.SavedDishId))
                    .Select(x => x.SavedDishId)
                    .Distinct()
                    .ToHashSet();

                var onlyValidDishes = topDishes.All(x => validDishIds.Contains(x.SavedDishId));

                return (atMost10 && allEntriesValid && sortedCorrectly && onlyValidDishes)
                    .Label($"AtMost10={atMost10}, AllEntriesValid={allEntriesValid}, " +
                           $"SortedCorrectly={sortedCorrectly}, OnlyValidDishes={onlyValidDishes}. " +
                           $"TopDishes count={topDishes.Count}");
            });
    }

    private static HousemateStatisticsHandler CreateSut(
        Guid householdId,
        List<Housemate> housemates,
        List<DayPlanDishLink> dishLinks,
        List<SavedDish> savedDishes,
        List<AttendanceRecord> attendanceRecords)
    {
        var attendanceRepositoryMock = new Mock<IAttendanceRepository>();
        var dayPlanDishLinkRepositoryMock = new Mock<IDayPlanDishLinkRepository>();
        var savedDishRepositoryMock = new Mock<ISavedDishRepository>();
        var housemateRepositoryMock = new Mock<IHousemateRepository>();

        attendanceRepositoryMock
            .Setup(x => x.GetAllByHouseholdAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attendanceRecords);

        dayPlanDishLinkRepositoryMock
            .Setup(x => x.GetAllByHouseholdAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dishLinks);

        savedDishRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedDishes);

        housemateRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(housemates);

        return new HousemateStatisticsHandler(
            attendanceRepositoryMock.Object,
            dayPlanDishLinkRepositoryMock.Object,
            savedDishRepositoryMock.Object,
            housemateRepositoryMock.Object);
    }

    private static Arbitrary<TopDishesScenario> TopDishesScenarioArb()
    {
        var gen = Gen.Choose(0, 15).SelectMany(dishCount =>
            Gen.Choose(0, 30).SelectMany(dayCount =>
                CreateTopDishesScenarioGen(dishCount, dayCount)));

        return Arb.From(gen);
    }

    private static Gen<TopDishesScenario> CreateTopDishesScenarioGen(
        int dishCount,
        int dayCount)
    {
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var baseDate = new DateOnly(2024, 1, 1);
        var from = baseDate;
        var to = baseDate.AddDays(Math.Max(dayCount - 1, 0));

        var housemate = new Housemate(
            housemateId, householdId, "TestHousemate",
            HousemateColors.Palette[0], false, 0);

        var housemates = new List<Housemate> { housemate };

        // Generate saved dishes with non-empty descriptions.
        var savedDishes = Enumerable.Range(0, dishCount)
            .Select(x => new SavedDish(
                Guid.NewGuid(),
                householdId,
                $"Dish {(char)('A' + (x % 26))}{x}",
                false))
            .ToList();

        if (dayCount == 0 || dishCount == 0)
        {
            return Gen.Constant(new TopDishesScenario(
                householdId,
                housemateId,
                from,
                to,
                new List<AttendanceRecord>(),
                new List<DayPlanDishLink>(),
                savedDishes,
                housemates));
        }

        // Generate random chef days (subset of all days in range).
        var allDayOffsets = Enumerable.Range(0, dayCount).ToArray();
        var chefDayCountGen = Gen.Choose(0, dayCount);

        // Generate a boolean for each (day, dish) pair to determine dish links.
        var totalPairs = dayCount * dishCount;
        var dishLinkDecisionGen = Gen.ListOf(Gen.Elements(true, false), totalPairs);

        return chefDayCountGen.SelectMany(chefDayCount =>
            Gen.Shuffle(allDayOffsets)
                .Select(x => x.Take(chefDayCount).ToList())
                .SelectMany(chefDayOffsets =>
                    dishLinkDecisionGen.Select(dishLinkDecisions =>
                    {
                        var attendanceRecords = chefDayOffsets
                            .Select(x => new AttendanceRecord(
                                householdId,
                                housemateId,
                                baseDate.AddDays(x),
                                AttendanceStatus.EatingIn,
                                true,
                                null))
                            .ToList();

                        // Build dish links from the boolean decisions.
                        var dishLinks = new List<DayPlanDishLink>();
                        var decisionIndex = 0;

                        for (var dayOffset = 0; dayOffset < dayCount; dayOffset++)
                        {
                            var sortOrder = 0;
                            for (var dishIndex = 0; dishIndex < dishCount; dishIndex++)
                            {
                                if (dishLinkDecisions[decisionIndex])
                                {
                                    dishLinks.Add(new DayPlanDishLink(
                                        householdId,
                                        baseDate.AddDays(dayOffset),
                                        savedDishes[dishIndex].Id,
                                        sortOrder));
                                    sortOrder++;
                                }
                                decisionIndex++;
                            }
                        }

                        return new TopDishesScenario(
                            householdId,
                            housemateId,
                            from,
                            to,
                            attendanceRecords,
                            dishLinks,
                            savedDishes,
                            housemates);
                    })));
    }

    private record TopDishesScenario(
        Guid HouseholdId,
        Guid HousemateId,
        DateOnly From,
        DateOnly To,
        List<AttendanceRecord> AttendanceRecords,
        List<DayPlanDishLink> DishLinks,
        List<SavedDish> SavedDishes,
        List<Housemate> Housemates);
}

// Feature: dish-statistics, Property 15: Housemate timeline sort order
/// <summary>Property-based tests for housemate timeline sort order in <see cref="HousemateStatisticsHandler"/>.</summary>
public class HousemateStatisticsHandlerTimelineSortOrderPropertyTests
{
    /// <summary>
    /// For any housemate timeline result containing multiple dish rows, the rows SHALL be ordered
    /// by all-time frequency descending, with alphabetical dish description ascending as tie-breaker.
    /// Validates: Requirements 8.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetStatisticsAsync_AnyData_TimelineEntriesSortedByFrequencyDescThenDescriptionAsc()
    {
        return Prop.ForAll(
            TimelineSortOrderScenarioArb(),
            async scenario =>
            {
                // Arrange.
                var sut = CreateSut(
                    scenario.HouseholdId,
                    scenario.Housemates,
                    scenario.DishLinks,
                    scenario.SavedDishes,
                    scenario.AttendanceRecords);

                // Act.
                var timelineEntries = await sut.GetTimelineAsync(
                    scenario.HouseholdId,
                    scenario.HousemateId,
                    scenario.TimelineFrom,
                    scenario.TimelineTo);

                // Assert — verify sort order: descending all-time frequency, ascending description as tie-breaker.
                var sortedCorrectly = true;
                for (var i = 1; i < timelineEntries.Entries.Count; i++)
                {
                    var previous = timelineEntries.Entries[i - 1];
                    var current = timelineEntries.Entries[i];

                    if (previous.AllTimeFrequency < current.AllTimeFrequency)
                    {
                        sortedCorrectly = false;
                        break;
                    }

                    if (previous.AllTimeFrequency == current.AllTimeFrequency &&
                        string.Compare(previous.DishDescription, current.DishDescription, StringComparison.OrdinalIgnoreCase) > 0)
                    {
                        sortedCorrectly = false;
                        break;
                    }
                }

                return sortedCorrectly
                    .Label($"Expected timeline entries sorted by AllTimeFrequency desc then DishDescription asc. " +
                           $"Got: [{string.Join(", ", timelineEntries.Entries.Select(x => $"{x.DishDescription}(freq={x.AllTimeFrequency})"))}]");
            });
    }

    private static HousemateStatisticsHandler CreateSut(
        Guid householdId,
        List<Housemate> housemates,
        List<DayPlanDishLink> dishLinks,
        List<SavedDish> savedDishes,
        List<AttendanceRecord> attendanceRecords)
    {
        var attendanceRepositoryMock = new Mock<IAttendanceRepository>();
        var dayPlanDishLinkRepositoryMock = new Mock<IDayPlanDishLinkRepository>();
        var savedDishRepositoryMock = new Mock<ISavedDishRepository>();
        var housemateRepositoryMock = new Mock<IHousemateRepository>();

        attendanceRepositoryMock
            .Setup(x => x.GetAllByHouseholdAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attendanceRecords);

        dayPlanDishLinkRepositoryMock
            .Setup(x => x.GetAllByHouseholdAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dishLinks);

        savedDishRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedDishes);

        housemateRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(housemates);

        return new HousemateStatisticsHandler(
            attendanceRepositoryMock.Object,
            dayPlanDishLinkRepositoryMock.Object,
            savedDishRepositoryMock.Object,
            housemateRepositoryMock.Object);
    }

    private static Arbitrary<TimelineSortOrderScenario> TimelineSortOrderScenarioArb()
    {
        var gen = Gen.Choose(2, 8).SelectMany(dishCount =>
            Gen.Choose(1, 30).SelectMany(dayCount =>
                CreateTimelineSortOrderScenarioGen(dishCount, dayCount)));

        return Arb.From(gen);
    }

    private static Gen<TimelineSortOrderScenario> CreateTimelineSortOrderScenarioGen(
        int dishCount,
        int dayCount)
    {
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var baseDate = new DateOnly(2024, 1, 1);
        var from = baseDate;
        var to = baseDate.AddDays(dayCount - 1);

        // Timeline window covers the full range.
        var timelineFrom = baseDate;
        var timelineTo = to;

        var housemate = new Housemate(
            housemateId, householdId, "TestHousemate",
            HousemateColors.Palette[0], false, 0);

        var housemates = new List<Housemate> { housemate };

        // Generate saved dishes with descriptions that can produce tie-breakers.
        var savedDishes = Enumerable.Range(0, dishCount)
            .Select(x => new SavedDish(
                Guid.NewGuid(),
                householdId,
                $"Dish {(char)('A' + (x % 26))}{x}",
                false))
            .ToList();

        // Generate which days are chef days (random subset, at least 1 to ensure timeline has entries).
        var allDayOffsets = Enumerable.Range(0, dayCount).ToArray();
        var chefDayCountGen = Gen.Choose(1, dayCount);

        // Generate a boolean for each (day, dish) pair to determine dish links.
        var totalPairs = dayCount * dishCount;
        var dishLinkDecisionGen = Gen.ListOf(Gen.Elements(true, false), totalPairs);

        return chefDayCountGen.SelectMany(chefDayCount =>
            Gen.Shuffle(allDayOffsets)
                .Select(x => x.Take(chefDayCount).ToList())
                .SelectMany(chefDayOffsets =>
                    dishLinkDecisionGen.Select(dishLinkDecisions =>
                    {
                        var attendanceRecords = chefDayOffsets
                            .Select(x => new AttendanceRecord(
                                householdId,
                                housemateId,
                                baseDate.AddDays(x),
                                AttendanceStatus.EatingIn,
                                true,
                                null))
                            .ToList();

                        // Build dish links from the boolean decisions.
                        var dishLinks = new List<DayPlanDishLink>();
                        var decisionIndex = 0;

                        for (var dayOffset = 0; dayOffset < dayCount; dayOffset++)
                        {
                            var sortOrder = 0;
                            for (var dishIndex = 0; dishIndex < dishCount; dishIndex++)
                            {
                                if (dishLinkDecisions[decisionIndex])
                                {
                                    dishLinks.Add(new DayPlanDishLink(
                                        householdId,
                                        baseDate.AddDays(dayOffset),
                                        savedDishes[dishIndex].Id,
                                        sortOrder));
                                    sortOrder++;
                                }
                                decisionIndex++;
                            }
                        }

                        return new TimelineSortOrderScenario(
                            householdId,
                            housemateId,
                            from,
                            to,
                            timelineFrom,
                            timelineTo,
                            attendanceRecords,
                            dishLinks,
                            savedDishes,
                            housemates);
                    })));
    }

    private record TimelineSortOrderScenario(
        Guid HouseholdId,
        Guid HousemateId,
        DateOnly From,
        DateOnly To,
        DateOnly TimelineFrom,
        DateOnly TimelineTo,
        List<AttendanceRecord> AttendanceRecords,
        List<DayPlanDishLink> DishLinks,
        List<SavedDish> SavedDishes,
        List<Housemate> Housemates);
}
