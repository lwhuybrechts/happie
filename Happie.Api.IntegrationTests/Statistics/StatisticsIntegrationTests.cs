using Azure.Data.Tables;
using Happie.Api.Domain;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure;
using Happie.Api.Infrastructure.Mappers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.IntegrationTests.Infrastructure;
using Happie.Shared.Domain;

namespace Happie.Api.IntegrationTests.Statistics;

/// <summary>Integration tests for DishStatisticsHandler and HousemateStatisticsHandler against Azurite.</summary>
public class StatisticsIntegrationTests
{
    private readonly ISavedDishRepository _savedDishRepository;
    private readonly IDayPlanDishLinkRepository _dayPlanDishLinkRepository;
    private readonly IHousemateRepository _housemateRepository;
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly IDishStatisticsHandler _dishStatisticsHandler;
    private readonly IHousemateStatisticsHandler _housemateStatisticsHandler;

    public StatisticsIntegrationTests()
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
            ?? "UseDevelopmentStorage=true";

        var tableServiceClient = new TableServiceClient(connectionString);

        TableHelper.TruncateTable(tableServiceClient, "SavedDishes");
        TableHelper.TruncateTable(tableServiceClient, "DayPlanDishLinks");
        TableHelper.TruncateTable(tableServiceClient, "Housemates");
        TableHelper.TruncateTable(tableServiceClient, "AttendanceRecords");

        var storageClient = new TableStorageClient(tableServiceClient);

        _savedDishRepository = new SavedDishRepository(storageClient, new SavedDishMapper());
        _dayPlanDishLinkRepository = new DayPlanDishLinkRepository(storageClient, new DayPlanDishLinkMapper());
        _housemateRepository = new HousemateRepository(storageClient, new HousemateMapper());
        _attendanceRepository = new AttendanceRepository(storageClient, new AttendanceRecordMapper());

        _dishStatisticsHandler = new DishStatisticsHandler(
            _attendanceRepository,
            _dayPlanDishLinkRepository,
            _savedDishRepository,
            _housemateRepository);

        _housemateStatisticsHandler = new HousemateStatisticsHandler(
            _attendanceRepository,
            _dayPlanDishLinkRepository,
            _savedDishRepository,
            _housemateRepository);
    }

    [Fact]
    public async Task GetDishStatistics_ReturnsCorrectResponseShape()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();

        await _savedDishRepository.UpsertAsync(new SavedDish(savedDishId, householdId, "Pasta", false));
        await _housemateRepository.UpsertAsync(new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false, 0));
        await _dayPlanDishLinkRepository.CreateAsync(new DayPlanDishLink(householdId, new DateOnly(2025, 6, 10), savedDishId, 0));
        await _dayPlanDishLinkRepository.CreateAsync(new DayPlanDishLink(householdId, new DateOnly(2025, 6, 12), savedDishId, 0));
        await _attendanceRepository.UpsertAsync(new AttendanceRecord(householdId, housemateId, new DateOnly(2025, 6, 10), AttendanceStatus.EatingIn, true, DateTimeOffset.UtcNow));
        await _attendanceRepository.UpsertAsync(new AttendanceRecord(householdId, housemateId, new DateOnly(2025, 6, 12), AttendanceStatus.EatingIn, true, DateTimeOffset.UtcNow));

        // Act.
        var result = await _dishStatisticsHandler.GetStatisticsAsync(
            householdId, savedDishId,
            new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 30));

        // Assert.
        Assert.Equal(2, result.TimesCooked);
        Assert.Equal(2, result.AllTimeTimesCooked);
        Assert.Equal(new DateOnly(2025, 6, 12), result.LastCookedDate);

        // Verify timeline separately.
        var timeline = await _dishStatisticsHandler.GetTimelineAsync(
            householdId, savedDishId,
            new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 30));
        Assert.Single(timeline.Entries);
        Assert.Equal(housemateId, timeline.Entries[0].HousemateId);
        Assert.Equal("Alice", timeline.Entries[0].HousemateName);
        Assert.Equal(HousemateColors.Palette[0], timeline.Entries[0].HousemateColor);
        Assert.Equal(2, timeline.Entries[0].CookingDays.Count);
    }

    [Fact]
    public async Task GetDishStatistics_SoftDeletedDish_ExcludesFromCounts()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var activeDishId = Guid.NewGuid();
        var deletedDishId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();

        await _savedDishRepository.UpsertAsync(new SavedDish(activeDishId, householdId, "Active Dish", false));
        await _savedDishRepository.UpsertAsync(new SavedDish(deletedDishId, householdId, "Deleted Dish", true));
        await _housemateRepository.UpsertAsync(new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false, 0));
        await _dayPlanDishLinkRepository.CreateAsync(new DayPlanDishLink(householdId, new DateOnly(2025, 7, 1), activeDishId, 0));
        await _dayPlanDishLinkRepository.CreateAsync(new DayPlanDishLink(householdId, new DateOnly(2025, 7, 2), deletedDishId, 0));
        await _attendanceRepository.UpsertAsync(new AttendanceRecord(householdId, housemateId, new DateOnly(2025, 7, 1), AttendanceStatus.EatingIn, true, DateTimeOffset.UtcNow));
        await _attendanceRepository.UpsertAsync(new AttendanceRecord(householdId, housemateId, new DateOnly(2025, 7, 2), AttendanceStatus.EatingIn, true, DateTimeOffset.UtcNow));

        // Act.
        var activeResult = await _dishStatisticsHandler.GetStatisticsAsync(
            householdId, activeDishId,
            new DateOnly(2025, 7, 1), new DateOnly(2025, 7, 31));

        var deletedResult = await _dishStatisticsHandler.GetStatisticsAsync(
            householdId, deletedDishId,
            new DateOnly(2025, 7, 1), new DateOnly(2025, 7, 31));

        // Assert.
        Assert.Equal(1, activeResult.TimesCooked);
        Assert.Equal(0, deletedResult.TimesCooked);
        Assert.Equal(0, deletedResult.AllTimeTimesCooked);
        Assert.Null(deletedResult.LastCookedDate);
    }

    [Fact]
    public async Task GetDishStatistics_DateRangeFiltering_OnlyCountsWithinRange()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();

        await _savedDishRepository.UpsertAsync(new SavedDish(savedDishId, householdId, "Salade", false));
        await _housemateRepository.UpsertAsync(new Housemate(housemateId, householdId, "Bob", HousemateColors.Palette[1], false, 0));

        // Days outside range.
        await _dayPlanDishLinkRepository.CreateAsync(new DayPlanDishLink(householdId, new DateOnly(2025, 5, 15), savedDishId, 0));
        await _dayPlanDishLinkRepository.CreateAsync(new DayPlanDishLink(householdId, new DateOnly(2025, 8, 1), savedDishId, 0));
        // Days inside range.
        await _dayPlanDishLinkRepository.CreateAsync(new DayPlanDishLink(householdId, new DateOnly(2025, 6, 10), savedDishId, 0));
        await _dayPlanDishLinkRepository.CreateAsync(new DayPlanDishLink(householdId, new DateOnly(2025, 6, 20), savedDishId, 0));

        await _attendanceRepository.UpsertAsync(new AttendanceRecord(householdId, housemateId, new DateOnly(2025, 6, 10), AttendanceStatus.EatingIn, true, DateTimeOffset.UtcNow));
        await _attendanceRepository.UpsertAsync(new AttendanceRecord(householdId, housemateId, new DateOnly(2025, 6, 20), AttendanceStatus.EatingIn, true, DateTimeOffset.UtcNow));

        // Act.
        var result = await _dishStatisticsHandler.GetStatisticsAsync(
            householdId, savedDishId,
            new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 30));

        // Assert.
        Assert.Equal(2, result.TimesCooked);
        Assert.Equal(4, result.AllTimeTimesCooked);
    }

    [Fact]
    public async Task GetHousemateStatistics_ReturnsCorrectResponseShape()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var otherHousemateId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();

        await _housemateRepository.UpsertAsync(new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false, 0));
        await _housemateRepository.UpsertAsync(new Housemate(otherHousemateId, householdId, "Bob", HousemateColors.Palette[1], false, 1));
        await _savedDishRepository.UpsertAsync(new SavedDish(savedDishId, householdId, "Risotto", false));

        // Alice is chef on 3 days.
        await _attendanceRepository.UpsertAsync(new AttendanceRecord(householdId, housemateId, new DateOnly(2025, 6, 10), AttendanceStatus.EatingIn, true, DateTimeOffset.UtcNow));
        await _attendanceRepository.UpsertAsync(new AttendanceRecord(householdId, housemateId, new DateOnly(2025, 6, 11), AttendanceStatus.EatingIn, true, DateTimeOffset.UtcNow));
        await _attendanceRepository.UpsertAsync(new AttendanceRecord(householdId, housemateId, new DateOnly(2025, 6, 12), AttendanceStatus.EatingIn, true, DateTimeOffset.UtcNow));
        // Bob is chef on 1 day.
        await _attendanceRepository.UpsertAsync(new AttendanceRecord(householdId, otherHousemateId, new DateOnly(2025, 6, 15), AttendanceStatus.EatingIn, true, DateTimeOffset.UtcNow));

        // Dish links on Alice's chef days.
        await _dayPlanDishLinkRepository.CreateAsync(new DayPlanDishLink(householdId, new DateOnly(2025, 6, 10), savedDishId, 0));
        await _dayPlanDishLinkRepository.CreateAsync(new DayPlanDishLink(householdId, new DateOnly(2025, 6, 11), savedDishId, 0));
        await _dayPlanDishLinkRepository.CreateAsync(new DayPlanDishLink(householdId, new DateOnly(2025, 6, 12), savedDishId, 0));

        // Act.
        var result = await _housemateStatisticsHandler.GetStatisticsAsync(
            householdId, housemateId,
            new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 30));

        // Assert.
        Assert.Equal(3, result.TimesCooked);
        Assert.Equal(3, result.AllTimeTimesCooked);
        Assert.Equal(3, result.DaysEatingIn);
        Assert.Equal(3, result.CookRatioDays);
        Assert.Equal(3, result.CookRatioEatingInDays);
        Assert.Equal(3, result.LongestStreak);
        Assert.True(result.BusiestWeek >= 2);
        Assert.Equal(2, result.CookingShares.Count);
        Assert.Single(result.TopDishes);
        Assert.Equal("Risotto", result.TopDishes[0].Description);
        Assert.Equal(3, result.TopDishes[0].Count);

        // Verify timeline separately.
        var timeline = await _housemateStatisticsHandler.GetTimelineAsync(
            householdId, housemateId,
            new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 30));
        Assert.NotEmpty(timeline.Entries);
    }

    [Fact]
    public async Task GetHousemateStatistics_SoftDeletedDish_ExcludedFromTopDishesAndTimeline()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var activeDishId = Guid.NewGuid();
        var deletedDishId = Guid.NewGuid();

        await _housemateRepository.UpsertAsync(new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false, 0));
        await _savedDishRepository.UpsertAsync(new SavedDish(activeDishId, householdId, "Active Dish", false));
        await _savedDishRepository.UpsertAsync(new SavedDish(deletedDishId, householdId, "Deleted Dish", true));

        // Chef on 2 days, one with active dish, one with deleted dish.
        await _attendanceRepository.UpsertAsync(new AttendanceRecord(householdId, housemateId, new DateOnly(2025, 7, 1), AttendanceStatus.EatingIn, true, DateTimeOffset.UtcNow));
        await _attendanceRepository.UpsertAsync(new AttendanceRecord(householdId, housemateId, new DateOnly(2025, 7, 2), AttendanceStatus.EatingIn, true, DateTimeOffset.UtcNow));

        await _dayPlanDishLinkRepository.CreateAsync(new DayPlanDishLink(householdId, new DateOnly(2025, 7, 1), activeDishId, 0));
        await _dayPlanDishLinkRepository.CreateAsync(new DayPlanDishLink(householdId, new DateOnly(2025, 7, 2), deletedDishId, 0));

        // Act.
        var result = await _housemateStatisticsHandler.GetStatisticsAsync(
            householdId, housemateId,
            new DateOnly(2025, 7, 1), new DateOnly(2025, 7, 31));

        // Assert — deleted dish is excluded from top dishes and timeline.
        Assert.Single(result.TopDishes);
        Assert.Equal("Active Dish", result.TopDishes[0].Description);

        // Verify timeline separately.
        var timeline = await _housemateStatisticsHandler.GetTimelineAsync(
            householdId, housemateId,
            new DateOnly(2025, 7, 1), new DateOnly(2025, 7, 31));
        Assert.DoesNotContain(timeline.Entries, x => x.SavedDishId == deletedDishId);
        Assert.Contains(timeline.Entries, x => x.SavedDishId == activeDishId);
    }

    [Fact]
    public async Task GetHousemateStatistics_DateRangeFiltering_OnlyCountsWithinRange()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();

        await _housemateRepository.UpsertAsync(new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false, 0));

        // Chef day outside range.
        await _attendanceRepository.UpsertAsync(new AttendanceRecord(householdId, housemateId, new DateOnly(2025, 5, 1), AttendanceStatus.EatingIn, true, DateTimeOffset.UtcNow));
        // Chef days inside range.
        await _attendanceRepository.UpsertAsync(new AttendanceRecord(householdId, housemateId, new DateOnly(2025, 6, 5), AttendanceStatus.EatingIn, true, DateTimeOffset.UtcNow));
        await _attendanceRepository.UpsertAsync(new AttendanceRecord(householdId, housemateId, new DateOnly(2025, 6, 6), AttendanceStatus.EatingIn, true, DateTimeOffset.UtcNow));

        // Act.
        var result = await _housemateStatisticsHandler.GetStatisticsAsync(
            householdId, housemateId,
            new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 30));

        // Assert.
        Assert.Equal(2, result.TimesCooked);
        Assert.Equal(3, result.AllTimeTimesCooked);
    }

    [Fact]
    public async Task GetDishStatistics_NoCookingDays_ReturnsZeros()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();

        await _savedDishRepository.UpsertAsync(new SavedDish(savedDishId, householdId, "Empty Dish", false));

        // Act.
        var result = await _dishStatisticsHandler.GetStatisticsAsync(
            householdId, savedDishId,
            new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 30));

        // Assert.
        Assert.Equal(0, result.TimesCooked);
        Assert.Equal(0, result.AllTimeTimesCooked);
        Assert.Null(result.LastCookedDate);

        // Verify timeline separately.
        var timeline = await _dishStatisticsHandler.GetTimelineAsync(
            householdId, savedDishId,
            new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 30));
        Assert.Empty(timeline.Entries);
    }
}
