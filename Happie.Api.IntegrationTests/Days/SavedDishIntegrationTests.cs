using Azure.Data.Tables;
using Happie.Api.Domain;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure;
using Happie.Api.Infrastructure.Mappers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.IntegrationTests.Infrastructure;
using Happie.Api.Results;
using Happie.Shared.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Happie.Api.IntegrationTests.Days;

/// <summary>Integration tests for saved dishes and multi-dish selection flows against Azurite.</summary>
public class SavedDishIntegrationTests
{
    private readonly ISavedDishRepository _savedDishRepository;
    private readonly IDishRepository _dishRepository;
    private readonly IDayPlanDishLinkRepository _dayPlanDishLinkRepository;
    private readonly IHousemateRepository _housemateRepository;
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly IDayHistoryRepository _dayHistoryRepository;
    private readonly SavedDishHandler _savedDishHandler;
    private readonly DayHandler _dayHandler;

    public SavedDishIntegrationTests()
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
            ?? "UseDevelopmentStorage=true";

        var tableServiceClient = new TableServiceClient(connectionString);

        TableHelper.TruncateTable(tableServiceClient, "SavedDishes");
        TableHelper.TruncateTable(tableServiceClient, "DayPlanDishLinks");
        TableHelper.TruncateTable(tableServiceClient, "DishRecords");
        TableHelper.TruncateTable(tableServiceClient, "Housemates");
        TableHelper.TruncateTable(tableServiceClient, "AttendanceRecords");
        TableHelper.TruncateTable(tableServiceClient, "Comments");
        TableHelper.TruncateTable(tableServiceClient, "DayHistory");

        var storageClient = new TableStorageClient(tableServiceClient);

        _savedDishRepository = new SavedDishRepository(storageClient, new SavedDishMapper());
        _dishRepository = new DishRepository(storageClient, new DishRecordMapper());
        _dayPlanDishLinkRepository = new DayPlanDishLinkRepository(storageClient, new DayPlanDishLinkMapper());
        _housemateRepository = new HousemateRepository(storageClient, new HousemateMapper());
        _attendanceRepository = new AttendanceRepository(storageClient, new AttendanceRecordMapper());
        _commentRepository = new CommentRepository(storageClient, new CommentMapper());
        _dayHistoryRepository = new DayHistoryRepository(storageClient, new DayHistoryEntryMapper());

        _savedDishHandler = new SavedDishHandler(
            _savedDishRepository,
            _dishRepository,
            _dayPlanDishLinkRepository,
            NullLogger<SavedDishHandler>.Instance);

        _dayHandler = new DayHandler(
            _housemateRepository,
            _attendanceRepository,
            _dishRepository,
            _commentRepository,
            _dayHistoryRepository,
            new NoOpPushHandler(),
            _savedDishRepository,
            _dayPlanDishLinkRepository);
    }

    [Fact]
    public async Task MultiDishSave_CreatesLinksAndResolvesDescription()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 8, 1);

        await _housemateRepository.UpsertAsync(new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false));
        var pastaResult = await _savedDishHandler.CreateAsync(householdId, "Pasta");
        var saladResult = await _savedDishHandler.CreateAsync(householdId, "Salade");

        // Act.
        var savedDishIds = new List<Guid> { pastaResult.SavedDish!.Id, saladResult.SavedDish!.Id };
        var result = await _dayHandler.UpsertDishAsync(householdId, date, null, savedDishIds, null, 0, housemateId);
        var dayPlan = await _dayHandler.GetDayPlanAsync(householdId, date);

        // Assert.
        Assert.Equal(DishUpsertResult.Success, result);
        Assert.NotNull(dayPlan.Dish);
        Assert.Equal("Pasta & Salade", dayPlan.Dish.Description);
        Assert.NotNull(dayPlan.Dish.SavedDishIds);
        Assert.Equal(2, dayPlan.Dish.SavedDishIds.Count);
        Assert.Equal(pastaResult.SavedDish.Id, dayPlan.Dish.SavedDishIds[0]);
        Assert.Equal(saladResult.SavedDish.Id, dayPlan.Dish.SavedDishIds[1]);
    }

    [Fact]
    public async Task MultiDishSave_ReplacesExistingLinks()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 8, 2);

        await _housemateRepository.UpsertAsync(new Housemate(housemateId, householdId, "Bob", HousemateColors.Palette[1], false));
        var dish1 = await _savedDishHandler.CreateAsync(householdId, "Rijst");
        var dish2 = await _savedDishHandler.CreateAsync(householdId, "Kip");
        var dish3 = await _savedDishHandler.CreateAsync(householdId, "Groente");

        // Save initial selection.
        await _dayHandler.UpsertDishAsync(householdId, date, null, new List<Guid> { dish1.SavedDish!.Id, dish2.SavedDish!.Id, dish3.SavedDish!.Id }, null, 0, housemateId);

        // Act — replace with different selection.
        var newIds = new List<Guid> { dish2.SavedDish!.Id, dish3.SavedDish!.Id };
        await _dayHandler.UpsertDishAsync(householdId, date, null, newIds, null, 0, housemateId);
        var dayPlan = await _dayHandler.GetDayPlanAsync(householdId, date);

        // Assert.
        Assert.NotNull(dayPlan.Dish);
        Assert.Equal("Kip & Groente", dayPlan.Dish.Description);
        Assert.Equal(2, dayPlan.Dish.SavedDishIds!.Count);
    }

    [Fact]
    public async Task AutoMatch_CustomDescriptionMatchesSavedDish_CreatesLink()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 8, 3);

        await _housemateRepository.UpsertAsync(new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false));
        var savedDish = await _savedDishHandler.CreateAsync(householdId, "Stamppot");

        // Act — save custom description matching the saved dish (case-insensitive).
        var result = await _dayHandler.UpsertDishAsync(householdId, date, "  STAMPPOT  ", null, null, 0, housemateId);
        var dayPlan = await _dayHandler.GetDayPlanAsync(householdId, date);

        // Assert.
        Assert.Equal(DishUpsertResult.Success, result);
        Assert.NotNull(dayPlan.Dish);
        Assert.Equal("Stamppot", dayPlan.Dish.Description);
        Assert.NotNull(dayPlan.Dish.SavedDishIds);
        Assert.Single(dayPlan.Dish.SavedDishIds);
        Assert.Equal(savedDish.SavedDish!.Id, dayPlan.Dish.SavedDishIds[0]);
    }

    [Fact]
    public async Task AutoMatch_SoftDeletedDish_ReactivatesAndCreatesLink()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 8, 4);

        await _housemateRepository.UpsertAsync(new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false));
        var created = await _savedDishHandler.CreateAsync(householdId, "Pannenkoeken");
        await _savedDishHandler.DeleteAsync(householdId, created.SavedDish!.Id);

        // Act — save custom description matching the soft-deleted dish.
        var result = await _dayHandler.UpsertDishAsync(householdId, date, "pannenkoeken", null, null, 0, housemateId);
        var dayPlan = await _dayHandler.GetDayPlanAsync(householdId, date);
        var reactivated = await _savedDishRepository.GetAsync(householdId, created.SavedDish.Id);

        // Assert.
        Assert.Equal(DishUpsertResult.Success, result);
        Assert.NotNull(reactivated);
        Assert.False(reactivated.IsDeleted);
        Assert.NotNull(dayPlan.Dish?.SavedDishIds);
        Assert.Single(dayPlan.Dish.SavedDishIds);
    }

    [Fact]
    public async Task RetroactiveConversion_SingleMatch_ConvertsDishRecord()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 6, 15);

        await _housemateRepository.UpsertAsync(new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false));

        // Create a custom dish record.
        await _dayHandler.UpsertDishAsync(householdId, date, "Lasagne", null, null, 0, housemateId);

        // Act — create a saved dish with the same name.
        var savedResult = await _savedDishHandler.CreateAsync(householdId, "Lasagne");

        // Assert — the DishRecord should now be linked.
        var dayPlan = await _dayHandler.GetDayPlanAsync(householdId, date);
        Assert.NotNull(dayPlan.Dish);
        Assert.Equal("Lasagne", dayPlan.Dish.Description);
        Assert.NotNull(dayPlan.Dish.SavedDishIds);
        Assert.Single(dayPlan.Dish.SavedDishIds);
        Assert.Equal(savedResult.SavedDish!.Id, dayPlan.Dish.SavedDishIds[0]);
    }

    [Fact]
    public async Task RetroactiveConversion_MultiPart_ConvertsWhenAllSegmentsAreSavedDishes()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 6, 20);

        await _housemateRepository.UpsertAsync(new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false));

        // Create a custom dish record with a multi-part description.
        await _dayHandler.UpsertDishAsync(householdId, date, "Worst & Kaas", null, null, 0, housemateId);

        // Create the first saved dish — "Worst" alone is not enough for multi-part conversion.
        var worstResult = await _savedDishHandler.CreateAsync(householdId, "Worst");

        // Verify NOT yet converted (only "Worst" exists, not "Kaas").
        var dayPlanBefore = await _dayHandler.GetDayPlanAsync(householdId, date);
        Assert.NotNull(dayPlanBefore.Dish);
        Assert.Null(dayPlanBefore.Dish.SavedDishIds);

        // Act — create the second saved dish ("Kaas"), now both segments exist.
        var kaasResult = await _savedDishHandler.CreateAsync(householdId, "Kaas");

        // Assert — the dish should now be converted to links.
        var dayPlanAfter = await _dayHandler.GetDayPlanAsync(householdId, date);
        Assert.NotNull(dayPlanAfter.Dish);
        Assert.Equal("Worst & Kaas", dayPlanAfter.Dish.Description);
        Assert.NotNull(dayPlanAfter.Dish.SavedDishIds);
        Assert.Equal(2, dayPlanAfter.Dish.SavedDishIds.Count);
        Assert.Equal(worstResult.SavedDish!.Id, dayPlanAfter.Dish.SavedDishIds[0]);
        Assert.Equal(kaasResult.SavedDish!.Id, dayPlanAfter.Dish.SavedDishIds[1]);
    }

    [Fact]
    public async Task RetroactiveConversion_DoesNotConvertRecordsWithExistingLinks()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 6, 25);

        await _housemateRepository.UpsertAsync(new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false));

        // Create a linked dish first (so the date already has links).
        var existingSaved = await _savedDishHandler.CreateAsync(householdId, "Soep");
        await _dayHandler.UpsertDishAsync(householdId, date, null, new List<Guid> { existingSaved.SavedDish!.Id }, null, 0, housemateId);

        // Manually update the DishRecord description to "Soep" (simulating a scenario where description matches but links exist).
        var existingDish = await _dishRepository.GetAsync(householdId, date);
        var updatedDish = existingDish! with { Description = "Soep" };
        await _dishRepository.UpsertAsync(updatedDish);

        // Act — create a new saved dish with the same name.
        var newSaved = await _savedDishHandler.CreateAsync(householdId, "Soep2");

        // Assert — the day should still have the original link, not be re-converted.
        var dayPlan = await _dayHandler.GetDayPlanAsync(householdId, date);
        Assert.NotNull(dayPlan.Dish?.SavedDishIds);
        Assert.Single(dayPlan.Dish.SavedDishIds);
        Assert.Equal(existingSaved.SavedDish.Id, dayPlan.Dish.SavedDishIds[0]);
    }

    [Fact]
    public async Task SoftDeletedDish_StillResolvesInDayPlan()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 8, 5);

        await _housemateRepository.UpsertAsync(new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false));
        var created = await _savedDishHandler.CreateAsync(householdId, "Hutspot");
        await _dayHandler.UpsertDishAsync(householdId, date, null, new List<Guid> { created.SavedDish!.Id }, null, 0, housemateId);

        // Act — soft-delete the saved dish.
        await _savedDishHandler.DeleteAsync(householdId, created.SavedDish.Id);
        var dayPlan = await _dayHandler.GetDayPlanAsync(householdId, date);

        // Assert — description still resolves from soft-deleted dish.
        Assert.NotNull(dayPlan.Dish);
        Assert.Equal("Hutspot", dayPlan.Dish.Description);
        Assert.NotNull(dayPlan.Dish.SavedDishIds);
        Assert.Single(dayPlan.Dish.SavedDishIds);
    }

    [Fact]
    public async Task RenameSavedDish_PropagatesWithoutDishRecordWrite()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 8, 6);

        await _housemateRepository.UpsertAsync(new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false));
        var created = await _savedDishHandler.CreateAsync(householdId, "Spagetti");
        await _dayHandler.UpsertDishAsync(householdId, date, null, new List<Guid> { created.SavedDish!.Id }, null, 0, housemateId);

        // Act — rename the saved dish.
        await _savedDishHandler.UpdateAsync(householdId, created.SavedDish.Id, "Spaghetti");
        var dayPlan = await _dayHandler.GetDayPlanAsync(householdId, date);

        // Assert — day plan shows the new name.
        Assert.NotNull(dayPlan.Dish);
        Assert.Equal("Spaghetti", dayPlan.Dish.Description);
    }

    [Fact]
    public async Task SelectionLimit_MoreThan10_ReturnsValidationError()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 8, 7);

        await _housemateRepository.UpsertAsync(new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false));

        var ids = new List<Guid>();
        for (var i = 0; i < 11; i++)
        {
            var result = await _savedDishHandler.CreateAsync(householdId, $"Dish{i:D2}");
            ids.Add(result.SavedDish!.Id);
        }

        // Act.
        var saveResult = await _dayHandler.UpsertDishAsync(householdId, date, null, ids, null, 0, housemateId);

        // Assert.
        Assert.Equal(DishUpsertResult.ValidationError, saveResult);
    }

    [Fact]
    public async Task MutualExclusion_BothSavedDishIdsAndDescription_ReturnsValidationError()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 8, 8);

        await _housemateRepository.UpsertAsync(new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false));
        var created = await _savedDishHandler.CreateAsync(householdId, "Risotto");

        // Act.
        var result = await _dayHandler.UpsertDishAsync(householdId, date, "Custom dish", new List<Guid> { created.SavedDish!.Id }, null, 0, housemateId);

        // Assert.
        Assert.Equal(DishUpsertResult.ValidationError, result);
    }

    [Fact]
    public async Task EmptySave_WithDinnerTime_PreservesDishRecordWithDinnerTime()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 8, 9);

        await _housemateRepository.UpsertAsync(new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false));

        // Save a dish with dinner time first.
        await _dayHandler.UpsertDishAsync(householdId, date, "Dinner", null, new TimeOnly(18, 30), 0, housemateId);

        // Act — clear the dish text but keep dinner time.
        await _dayHandler.UpsertDishAsync(householdId, date, null, null, new TimeOnly(18, 30), 0, housemateId);
        var dayPlan = await _dayHandler.GetDayPlanAsync(householdId, date);

        // Assert — dish record preserved with dinner time but empty description.
        Assert.NotNull(dayPlan.Dish);
        Assert.Equal(string.Empty, dayPlan.Dish.Description);
        Assert.Equal(18, dayPlan.Dish.DinnerTimeHour);
        Assert.Equal(30, dayPlan.Dish.DinnerTimeMinute);
    }

    [Fact]
    public async Task ResaveSameLinkedDishes_NoPhantomHistoryEntry()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 8, 10);

        await _housemateRepository.UpsertAsync(new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false));
        var dish1 = await _savedDishHandler.CreateAsync(householdId, "Brood");
        var dish2 = await _savedDishHandler.CreateAsync(householdId, "Kaas");

        var ids = new List<Guid> { dish1.SavedDish!.Id, dish2.SavedDish!.Id };

        // Save the first time.
        await _dayHandler.UpsertDishAsync(householdId, date, null, ids, null, 0, housemateId);

        // Check history count after first save.
        var historyAfterFirst = await _dayHistoryRepository.GetByDateAsync(householdId, date);
        var dishHistoryCountAfterFirst = historyAfterFirst.Count(x => x.ChangeType == ChangeType.Dish || x.ChangeType == ChangeType.DishAndDinnerTime);

        // Act — save the same IDs again.
        await _dayHandler.UpsertDishAsync(householdId, date, null, ids, null, 0, housemateId);

        // Assert — no new dish history entry added.
        var historyAfterSecond = await _dayHistoryRepository.GetByDateAsync(householdId, date);
        var dishHistoryCountAfterSecond = historyAfterSecond.Count(x => x.ChangeType == ChangeType.Dish || x.ChangeType == ChangeType.DishAndDinnerTime);

        Assert.Equal(dishHistoryCountAfterFirst, dishHistoryCountAfterSecond);
    }
}
