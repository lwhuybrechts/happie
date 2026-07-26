using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Domain;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Shared.Domain;
using Moq;

namespace Happie.Api.Tests.Handlers;

// Feature: dish-statistics, Property 5: Color attribution correctness
// Feature: dish-statistics, Property 3: Dish timeline dot correctness
/// <summary>Property-based tests for <see cref="DishStatisticsHandler"/>.</summary>
public class DishStatisticsHandlerPropertyTests
{
    /// <summary>
    /// For any set of housemates with assigned colors, the color in each dish timeline entry
    /// SHALL exactly match the corresponding housemate's Color field from the Housemate record.
    /// Validates: Requirements 3.4, 6.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetStatisticsAsync_AnyHousemates_TimelineEntryColorsMatchHousemateColors()
    {
        return Prop.ForAll(
            HousemateListArb(),
            async housemates =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var savedDishId = Guid.NewGuid();
                var from = new DateOnly(2024, 1, 1);
                var to = new DateOnly(2024, 12, 31);
                var timelineFrom = new DateOnly(2024, 1, 1);
                var timelineTo = new DateOnly(2024, 12, 31);

                var savedDish = new SavedDish(savedDishId, householdId, "Test Dish", false);

                // Create a DayPlanDishLink for the dish on a date within the range.
                var cookingDate = new DateOnly(2024, 6, 15);
                var dishLinks = new List<DayPlanDishLink>
                {
                    new(householdId, cookingDate, savedDishId, 0)
                };

                // Create attendance records with IsChef=true for each housemate on that date.
                var attendanceRecords = housemates
                    .Select(x => new AttendanceRecord(
                        householdId, x.Id, cookingDate, AttendanceStatus.EatingIn, true, null))
                    .ToList();

                var sut = CreateSut(
                    householdId,
                    housemates,
                    dishLinks,
                    new List<SavedDish> { savedDish },
                    attendanceRecords);

                // Act.
                var result = await sut.GetTimelineAsync(
                    householdId, savedDishId, timelineFrom, timelineTo);

                // Assert.
                var housemateColorById = housemates.ToDictionary(x => x.Id, x => x.Color);

                var allColorsMatch = result.Entries.All(x =>
                    housemateColorById.ContainsKey(x.HousemateId) &&
                    x.HousemateColor == housemateColorById[x.HousemateId]);

                return allColorsMatch
                    .Label($"Expected all timeline entry colors to match housemate Color fields. " +
                           $"Entries: {string.Join(", ", result.Entries.Select(x => $"{x.HousemateId}={x.HousemateColor}"))}");
            });
    }

    /// <summary>
    /// For any set of housemates with assigned colors, the color in each cooking share entry
    /// from HousemateStatisticsHandler SHALL exactly match that housemate's Color field.
    /// Validates: Requirements 3.4, 6.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetStatisticsAsync_AnyHousemates_CookingShareColorsMatchHousemateColors()
    {
        return Prop.ForAll(
            HousemateListArb(),
            async housemates =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var targetHousemate = housemates[0];
                var from = new DateOnly(2024, 1, 1);
                var to = new DateOnly(2024, 12, 31);
                var timelineFrom = new DateOnly(2024, 1, 1);
                var timelineTo = new DateOnly(2024, 12, 31);

                var savedDishId = Guid.NewGuid();
                var savedDish = new SavedDish(savedDishId, householdId, "Test Dish", false);

                // Create attendance records with IsChef=true for each housemate on a date.
                var cookingDate = new DateOnly(2024, 6, 15);
                var attendanceRecords = housemates
                    .Select(x => new AttendanceRecord(
                        householdId, x.Id, cookingDate, AttendanceStatus.EatingIn, true, null))
                    .ToList();

                var dishLinks = new List<DayPlanDishLink>
                {
                    new(householdId, cookingDate, savedDishId, 0)
                };

                var housemateStatisticsSut = CreateHousemateStatisticsSut(
                    householdId,
                    housemates,
                    dishLinks,
                    new List<SavedDish> { savedDish },
                    attendanceRecords);

                // Act.
                var result = await housemateStatisticsSut.GetStatisticsAsync(
                    householdId, targetHousemate.Id, from, to);

                // Assert.
                var housemateColorById = housemates.ToDictionary(x => x.Id, x => x.Color);

                var allColorsMatch = result.CookingShares.All(x =>
                    housemateColorById.ContainsKey(x.HousemateId) &&
                    x.HousemateColor == housemateColorById[x.HousemateId]);

                return allColorsMatch
                    .Label($"Expected all cooking share colors to match housemate Color fields. " +
                           $"Shares: {string.Join(", ", result.CookingShares.Select(x => $"{x.HousemateId}={x.HousemateColor}"))}");
            });
    }

    /// <summary>
    /// For any dish and set of household data, each housemate's cooking days in the dish timeline
    /// SHALL be exactly the set of dates where that housemate had IsChef=true AND a DayPlanDishLink
    /// exists for the dish on that date, within the timeline window. Only housemates with at least
    /// one such date SHALL appear in the timeline.
    /// Validates: Requirements 3.2, 3.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetStatisticsAsync_TimelineDots_MatchChefDaysWithDishLinks()
    {
        return Prop.ForAll(
            TimelineScenarioArb(),
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
                    scenario.SavedDishId,
                    scenario.TimelineFrom,
                    scenario.TimelineTo);

                // Assert — compute expected timeline dots independently.
                var dishDatesInWindow = scenario.DishLinks
                    .Where(x => x.SavedDishId == scenario.SavedDishId)
                    .Where(x => x.Date >= scenario.TimelineFrom && x.Date <= scenario.TimelineTo)
                    .Select(x => x.Date)
                    .Distinct()
                    .ToHashSet();

                var expectedByHousemate = scenario.AttendanceRecords
                    .Where(x => x.IsChef)
                    .Where(x => x.Date >= scenario.TimelineFrom && x.Date <= scenario.TimelineTo)
                    .Where(x => dishDatesInWindow.Contains(x.Date))
                    .GroupBy(x => x.HousemateId)
                    .Where(x => scenario.NonDeletedHousemateIds.Contains(x.Key))
                    .ToDictionary(
                        x => x.Key,
                        x => x.Select(r => r.Date).Distinct().OrderBy(d => d).ToList());

                // Only non-deleted housemates should appear (all of them, with or without cooking days).
                var expectedHousemateIds = scenario.NonDeletedHousemateIds;

                var actualHousemateIds = result.Entries
                    .Select(x => x.HousemateId)
                    .ToHashSet();

                var housemateSetCorrect = expectedHousemateIds.SetEquals(actualHousemateIds);

                // Verify each housemate's cooking days match exactly.
                var dotsCorrect = result.Entries.All(entry =>
                {
                    var expectedDays = expectedByHousemate.TryGetValue(entry.HousemateId, out var days)
                        ? days
                        : new List<DateOnly>();

                    var actualDays = entry.CookingDays.OrderBy(x => x).ToList();
                    return actualDays.SequenceEqual(expectedDays);
                });

                return (housemateSetCorrect && dotsCorrect)
                    .Label($"HousemateSetCorrect={housemateSetCorrect}, DotsCorrect={dotsCorrect}, " +
                           $"ExpectedHousemates={expectedHousemateIds.Count}, ActualHousemates={actualHousemateIds.Count}");
            });
    }

    private static DishStatisticsHandler CreateSut(
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

        return new DishStatisticsHandler(
            attendanceRepositoryMock.Object,
            dayPlanDishLinkRepositoryMock.Object,
            savedDishRepositoryMock.Object,
            housemateRepositoryMock.Object);
    }

    private static HousemateStatisticsHandler CreateHousemateStatisticsSut(
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

    private static Arbitrary<List<Housemate>> HousemateListArb()
    {
        var gen = Gen.Choose(1, 6)
            .SelectMany(count =>
                HousemateGen(0).ArrayOf(count)
                    .Select(x =>
                    {
                        // Assign unique sort orders after generation.
                        return x.Select((housemate, index) =>
                            housemate with { SortOrder = index }).ToList();
                    }));

        return Arb.From(gen);
    }

    private static Gen<Housemate> HousemateGen(int index)
    {
        return Gen.Choose(1, 10)
            .SelectMany(nameLength =>
                Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
                             'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T')
                    .ArrayOf(nameLength)
                    .Select(chars => new string(chars)))
            .SelectMany(name =>
                Gen.Elements(HousemateColors.Palette.ToArray())
                    .Select(color => new Housemate(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        name,
                        color,
                        false,
                        index)));
    }

    private static Arbitrary<TimelineScenario> TimelineScenarioArb()
    {
        var gen = Gen.Choose(1, 5).SelectMany(housemateCount =>
            Gen.Choose(0, 10).SelectMany(linkCount =>
                Gen.Choose(0, 15).SelectMany(attendanceCount =>
                    Gen.Choose(1, 30).SelectMany(timelineWindowDays =>
                        CreateScenarioGen(housemateCount, linkCount, attendanceCount, timelineWindowDays)))));

        return Arb.From(gen);
    }

    private static Gen<TimelineScenario> CreateScenarioGen(
        int housemateCount,
        int linkCount,
        int attendanceCount,
        int timelineWindowDays)
    {
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var baseDate = new DateOnly(2024, 6, 1);
        var timelineFrom = baseDate;
        var timelineTo = baseDate.AddDays(timelineWindowDays);

        // Generate housemates.
        var housemateIds = Enumerable.Range(0, housemateCount).Select(_ => Guid.NewGuid()).ToList();
        var housemates = housemateIds.Select((id, index) =>
            new Housemate(id, householdId, $"Housemate{index}", HousemateColors.Palette[index % HousemateColors.Palette.Count], false, index)).ToList();

        // Generate dish links within the timeline window.
        var dishLinkGen = Gen.Choose(0, timelineWindowDays).SelectMany(dayOffset =>
            Gen.Elements(true, false).Select(isTargetDish =>
            {
                var date = baseDate.AddDays(dayOffset);
                var dishId = isTargetDish ? savedDishId : Guid.NewGuid();
                return new DayPlanDishLink(householdId, date, dishId, 0);
            }));

        // Generate attendance records within the timeline window.
        var attendanceGen = Gen.Choose(0, housemateCount - 1).SelectMany(housemateIndex =>
            Gen.Choose(0, timelineWindowDays).SelectMany(dayOffset =>
                Gen.Elements(true, false).Select(isChef =>
                {
                    var date = baseDate.AddDays(dayOffset);
                    var housemateId = housemateIds[housemateIndex];
                    return new AttendanceRecord(
                        householdId,
                        housemateId,
                        date,
                        AttendanceStatus.EatingIn,
                        isChef,
                        null);
                })));

        return dishLinkGen.ArrayOf(linkCount).SelectMany(dishLinks =>
            attendanceGen.ArrayOf(attendanceCount).Select(attendanceRecords =>
            {
                var savedDishes = new List<SavedDish>
                {
                    new(savedDishId, householdId, "Target Dish", false)
                };

                return new TimelineScenario(
                    householdId,
                    savedDishId,
                    timelineFrom,
                    timelineTo,
                    dishLinks.ToList(),
                    attendanceRecords.ToList(),
                    savedDishes,
                    housemates,
                    housemateIds.ToHashSet());
            }));
    }

    private record TimelineScenario(
        Guid HouseholdId,
        Guid SavedDishId,
        DateOnly TimelineFrom,
        DateOnly TimelineTo,
        List<DayPlanDishLink> DishLinks,
        List<AttendanceRecord> AttendanceRecords,
        List<SavedDish> SavedDishes,
        List<Housemate> Housemates,
        HashSet<Guid> NonDeletedHousemateIds);

    // Feature: dish-statistics, Property 16: Soft-delete exclusion

    /// <summary>
    /// For any statistics computation (dish or housemate), all DayPlanDishLink records referencing
    /// a SavedDish with IsDeleted=true SHALL be excluded from all counts, timeline dots, top dishes,
    /// and cooking share values. No deleted dish SHALL appear in any timeline row.
    /// Validates: Requirements 9.1, 9.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetStatisticsAsync_DeletedDishes_ExcludedFromDishCounts()
    {
        return Prop.ForAll(
            SoftDeleteScenarioArb(),
            async scenario =>
            {
                // Arrange.
                var sut = CreateSut(
                    scenario.HouseholdId,
                    scenario.Housemates,
                    scenario.DishLinks,
                    scenario.SavedDishes,
                    scenario.AttendanceRecords);

                // Act — query statistics for the deleted dish.
                var result = await sut.GetStatisticsAsync(
                    scenario.HouseholdId,
                    scenario.DeletedDishId,
                    scenario.From,
                    scenario.To);

                // Also query timeline for the deleted dish.
                var timelineResult = await sut.GetTimelineAsync(
                    scenario.HouseholdId,
                    scenario.DeletedDishId,
                    scenario.From,
                    scenario.To);

                // Assert — deleted dish should yield zero counts and no timeline entries.
                var timesCookedZero = result.TimesCooked == 0;
                var allTimeCookedZero = result.AllTimeTimesCooked == 0;
                var lastCookedNull = result.LastCookedDate is null;
                var noTimelineEntries = timelineResult.Entries.Count == 0;

                return (timesCookedZero && allTimeCookedZero && lastCookedNull && noTimelineEntries)
                    .Label($"Deleted dish should have zero stats. " +
                           $"TimesCooked={result.TimesCooked}, AllTime={result.AllTimeTimesCooked}, " +
                           $"LastCooked={result.LastCookedDate}, TimelineCount={timelineResult.Entries.Count}");
            });
    }

    /// <summary>
    /// For any dish statistics computation, dish links referencing deleted dishes SHALL NOT
    /// be included in the counts for non-deleted dishes. Only links to non-deleted dishes count.
    /// Validates: Requirements 9.1, 9.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetStatisticsAsync_NonDeletedDish_OnlyCountsNonDeletedLinks()
    {
        return Prop.ForAll(
            SoftDeleteScenarioArb(),
            async scenario =>
            {
                // Arrange.
                var sut = CreateSut(
                    scenario.HouseholdId,
                    scenario.Housemates,
                    scenario.DishLinks,
                    scenario.SavedDishes,
                    scenario.AttendanceRecords);

                // Act — query statistics for the non-deleted dish.
                var result = await sut.GetStatisticsAsync(
                    scenario.HouseholdId,
                    scenario.NonDeletedDishId,
                    scenario.From,
                    scenario.To);

                // Assert — count should match only links for the non-deleted dish.
                var expectedDates = scenario.DishLinks
                    .Where(x => x.SavedDishId == scenario.NonDeletedDishId)
                    .Select(x => x.Date)
                    .Distinct()
                    .Where(x => x >= scenario.From && x <= scenario.To)
                    .ToList();

                var timesCookedCorrect = result.TimesCooked == expectedDates.Count;

                var expectedAllTimeDates = scenario.DishLinks
                    .Where(x => x.SavedDishId == scenario.NonDeletedDishId)
                    .Select(x => x.Date)
                    .Distinct()
                    .ToList();

                var allTimeCookedCorrect = result.AllTimeTimesCooked == expectedAllTimeDates.Count;

                return (timesCookedCorrect && allTimeCookedCorrect)
                    .Label($"Non-deleted dish counts should reflect only its own links. " +
                           $"TimesCooked: expected={expectedDates.Count} actual={result.TimesCooked}, " +
                           $"AllTime: expected={expectedAllTimeDates.Count} actual={result.AllTimeTimesCooked}");
            });
    }

    /// <summary>
    /// For housemate statistics, deleted dishes SHALL NOT appear in timeline rows
    /// and SHALL NOT be counted in top dishes.
    /// Validates: Requirements 9.1, 9.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetStatisticsAsync_Housemate_DeletedDishesExcludedFromTimelineAndTopDishes()
    {
        return Prop.ForAll(
            SoftDeleteScenarioArb(),
            async scenario =>
            {
                // Arrange.
                var targetHousemate = scenario.Housemates[0];

                var housemateStatisticsSut = CreateHousemateStatisticsSut(
                    scenario.HouseholdId,
                    scenario.Housemates,
                    scenario.DishLinks,
                    scenario.SavedDishes,
                    scenario.AttendanceRecords);

                // Act.
                var result = await housemateStatisticsSut.GetStatisticsAsync(
                    scenario.HouseholdId,
                    targetHousemate.Id,
                    scenario.From,
                    scenario.To);

                // Also query timeline for this housemate.
                var timelineResult = await housemateStatisticsSut.GetTimelineAsync(
                    scenario.HouseholdId,
                    targetHousemate.Id,
                    scenario.From,
                    scenario.To);

                // Assert — no timeline row should reference the deleted dish.
                var deletedDishIds = scenario.SavedDishes
                    .Where(x => x.IsDeleted)
                    .Select(x => x.Id)
                    .ToHashSet();

                var noDeletedInTimeline = timelineResult.Entries
                    .All(x => !deletedDishIds.Contains(x.SavedDishId));

                var noDeletedInTopDishes = result.TopDishes
                    .All(x => !deletedDishIds.Contains(x.SavedDishId));

                return (noDeletedInTimeline && noDeletedInTopDishes)
                    .Label($"Deleted dishes should not appear in housemate stats. " +
                           $"NoDeletedInTimeline={noDeletedInTimeline}, NoDeletedInTopDishes={noDeletedInTopDishes}, " +
                           $"TimelineCount={timelineResult.Entries.Count}, TopDishCount={result.TopDishes.Count}");
            });
    }

    private static Arbitrary<SoftDeleteScenario> SoftDeleteScenarioArb()
    {
        var gen = Gen.Choose(1, 5).SelectMany(housemateCount =>
            Gen.Choose(1, 10).SelectMany(linkCountPerDish =>
                Gen.Choose(0, 20).SelectMany(attendanceCount =>
                    CreateSoftDeleteScenarioGen(housemateCount, linkCountPerDish, attendanceCount))));

        return Arb.From(gen);
    }

    private static Gen<SoftDeleteScenario> CreateSoftDeleteScenarioGen(
        int housemateCount,
        int linkCountPerDish,
        int attendanceCount)
    {
        var householdId = Guid.NewGuid();
        var deletedDishId = Guid.NewGuid();
        var nonDeletedDishId = Guid.NewGuid();
        var baseDate = new DateOnly(2024, 3, 1);
        var from = baseDate;
        var to = baseDate.AddDays(60);

        // Generate housemates.
        var housemateIds = Enumerable.Range(0, housemateCount).Select(_ => Guid.NewGuid()).ToList();
        var housemates = housemateIds.Select((id, index) =>
            new Housemate(
                id,
                householdId,
                $"Housemate{index}",
                HousemateColors.Palette[index % HousemateColors.Palette.Count],
                false,
                index)).ToList();

        var savedDishes = new List<SavedDish>
        {
            new(deletedDishId, householdId, "Deleted Dish", true),
            new(nonDeletedDishId, householdId, "Active Dish", false)
        };

        // Generate dish links for both deleted and non-deleted dishes within [from, to].
        var dishLinkGen = Gen.Choose(0, 60).SelectMany(dayOffset =>
            Gen.Elements(deletedDishId, nonDeletedDishId).Select(dishId =>
                new DayPlanDishLink(householdId, baseDate.AddDays(dayOffset), dishId, 0)));

        // Generate attendance records — ensure the first housemate has chef days.
        var attendanceGen = Gen.Choose(0, housemateCount - 1).SelectMany(housemateIndex =>
            Gen.Choose(0, 60).SelectMany(dayOffset =>
                Gen.Elements(true, false).Select(isChef =>
                    new AttendanceRecord(
                        householdId,
                        housemateIds[housemateIndex],
                        baseDate.AddDays(dayOffset),
                        AttendanceStatus.EatingIn,
                        isChef,
                        null))));

        return dishLinkGen.ArrayOf(linkCountPerDish * 2).SelectMany(dishLinks =>
            attendanceGen.ArrayOf(attendanceCount).Select(attendanceRecords =>
                new SoftDeleteScenario(
                    householdId,
                    deletedDishId,
                    nonDeletedDishId,
                    from,
                    to,
                    dishLinks.ToList(),
                    attendanceRecords.ToList(),
                    savedDishes,
                    housemates)));
    }

    private record SoftDeleteScenario(
        Guid HouseholdId,
        Guid DeletedDishId,
        Guid NonDeletedDishId,
        DateOnly From,
        DateOnly To,
        List<DayPlanDishLink> DishLinks,
        List<AttendanceRecord> AttendanceRecords,
        List<SavedDish> SavedDishes,
        List<Housemate> Housemates);
}
