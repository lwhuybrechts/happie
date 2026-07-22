using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Domain;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Results;
using Moq;

namespace Happie.Api.Tests.Handlers;

// Feature: happie, Property 3: Save creates and replaces links correctly
/// <summary>
/// Property-based tests for <see cref="DayHandler.UpsertDishAsync"/> that validate
/// link creation and replacement behavior across saved-mode, custom-mode, and empty saves.
/// Validates: Requirements 5.2, 5.3, 5.5, 5.7
/// </summary>
public class DayHandlerDishSaveLinksPropertyTests
{
    private readonly Mock<IHousemateRepository> _housemateRepositoryMock = new();
    private readonly Mock<IAttendanceRepository> _attendanceRepositoryMock = new();
    private readonly Mock<IDishRepository> _dishRepositoryMock = new();
    private readonly Mock<ICommentRepository> _commentRepositoryMock = new();
    private readonly Mock<IDayHistoryRepository> _dayHistoryRepositoryMock = new();
    private readonly Mock<IPushHandler> _pushHandlerMock = new();
    private readonly Mock<ISavedDishRepository> _savedDishRepositoryMock = new();
    private readonly Mock<IDayPlanDishLinkRepository> _dayPlanDishLinkRepositoryMock = new();
    private readonly DayHandler _sut;

    /// <summary>Initializes a new instance of <see cref="DayHandlerDishSaveLinksPropertyTests"/>.</summary>
    public DayHandlerDishSaveLinksPropertyTests()
    {
        _sut = new DayHandler(
            _housemateRepositoryMock.Object,
            _attendanceRepositoryMock.Object,
            _dishRepositoryMock.Object,
            _commentRepositoryMock.Object,
            _dayHistoryRepositoryMock.Object,
            _pushHandlerMock.Object,
            _savedDishRepositoryMock.Object,
            _dayPlanDishLinkRepositoryMock.Object);
    }

    /// <summary>
    /// For any valid saved-mode save (1–10 unique IDs, all existing in household):
    /// ReplaceAllAsync is called with correct links (SortOrder = list index),
    /// and UpsertAsync is called with empty description.
    /// **Validates: Requirements 5.2, 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_SavedMode_ReplacesLinksAndClearsDescription()
    {
        return Prop.ForAll(
            SavedModeScenarioArb(),
            async scenario =>
            {
                // Reset mocks.
                ResetAllMocks();

                SetupSavedDishRepository(scenario.HouseholdId, scenario.SavedDishes);
                SetupDishRepositoryGet(scenario.HouseholdId, scenario.Date, scenario.ExistingDishRecord);
                SetupDishRepositoryUpsert();
                SetupDayPlanDishLinkRepositoryReplaceAll();
                SetupDayHistoryRepository();
                SetupPushHandler();

                // Act.
                var result = await _sut.UpsertDishAsync(
                    scenario.HouseholdId,
                    scenario.Date,
                    null,
                    scenario.SavedDishIds,
                    null,
                    0,
                    scenario.ActingHousemateId);

                // Assert.
                var expectedLinks = scenario.SavedDishIds
                    .Select((x, index) => new DayPlanDishLink(scenario.HouseholdId, scenario.Date, x, index))
                    .ToList();

                var replaceAllCalled = false;
                try
                {
                    _dayPlanDishLinkRepositoryMock.Verify(
                        x => x.ReplaceAllAsync(
                            scenario.HouseholdId,
                            scenario.Date,
                            It.Is<IReadOnlyList<DayPlanDishLink>>(links =>
                                links.Count == expectedLinks.Count &&
                                links.Select((l, i) => l.SavedDishId == expectedLinks[i].SavedDishId && l.SortOrder == i).All(v => v)),
                            It.IsAny<CancellationToken>()),
                        Times.Once());
                    replaceAllCalled = true;
                }
                catch { }

                var upsertCalledWithEmptyDescription = false;
                try
                {
                    _dishRepositoryMock.Verify(
                        x => x.UpsertAsync(
                            It.Is<DishRecord>(r => r.HouseholdId == scenario.HouseholdId && r.Date == scenario.Date && r.Description == string.Empty),
                            It.IsAny<CancellationToken>()),
                        Times.Once());
                    upsertCalledWithEmptyDescription = true;
                }
                catch { }

                return (result == DishUpsertResult.Success && replaceAllCalled && upsertCalledWithEmptyDescription)
                    .Label($"Result={result}, ReplaceAllCalled={replaceAllCalled}, UpsertEmptyDesc={upsertCalledWithEmptyDescription}");
            });
    }

    /// <summary>
    /// For any custom-mode save (null/empty savedDishIds with non-empty description that does NOT match any saved dish):
    /// DeleteAllAsync is called, and UpsertAsync is called with trimmed description.
    /// **Validates: Requirements 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_CustomModeNoMatch_DeletesLinksAndStoresDescription()
    {
        return Prop.ForAll(
            CustomModeNoMatchScenarioArb(),
            async scenario =>
            {
                // Reset mocks.
                ResetAllMocks();

                SetupSavedDishRepository(scenario.HouseholdId, scenario.SavedDishes);
                SetupDishRepositoryGet(scenario.HouseholdId, scenario.Date, null);
                SetupDishRepositoryUpsert();
                SetupDayPlanDishLinkRepositoryDeleteAll();
                SetupDayHistoryRepository();
                SetupPushHandler();

                // Act.
                var result = await _sut.UpsertDishAsync(
                    scenario.HouseholdId,
                    scenario.Date,
                    scenario.Description,
                    null,
                    null,
                    0,
                    scenario.ActingHousemateId);

                // Assert.
                var deleteAllCalled = false;
                try
                {
                    _dayPlanDishLinkRepositoryMock.Verify(
                        x => x.DeleteAllAsync(scenario.HouseholdId, scenario.Date, It.IsAny<CancellationToken>()),
                        Times.Once());
                    deleteAllCalled = true;
                }
                catch { }

                var trimmedDescription = scenario.Description.Trim();
                var upsertCalledWithTrimmedDescription = false;
                try
                {
                    _dishRepositoryMock.Verify(
                        x => x.UpsertAsync(
                            It.Is<DishRecord>(r => r.HouseholdId == scenario.HouseholdId && r.Date == scenario.Date && r.Description == trimmedDescription),
                            It.IsAny<CancellationToken>()),
                        Times.Once());
                    upsertCalledWithTrimmedDescription = true;
                }
                catch { }

                return (result == DishUpsertResult.Success && deleteAllCalled && upsertCalledWithTrimmedDescription)
                    .Label($"Result={result}, DeleteAllCalled={deleteAllCalled}, UpsertTrimmedDesc={upsertCalledWithTrimmedDescription}");
            });
    }

    /// <summary>
    /// For any empty save (null/empty savedDishIds AND null/empty description):
    /// DeleteAllAsync is called. If DishRecord has no DinnerTime, DeleteAsync is called.
    /// If DishRecord has DinnerTime, UpsertAsync is called with empty description.
    /// **Validates: Requirements 5.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_EmptySave_DeletesLinksAndHandlesDishRecord()
    {
        return Prop.ForAll(
            EmptySaveScenarioArb(),
            async scenario =>
            {
                // Reset mocks.
                ResetAllMocks();

                SetupDishRepositoryGet(scenario.HouseholdId, scenario.Date, scenario.ExistingDishRecord);
                SetupDishRepositoryUpsert();
                SetupDishRepositoryDelete();
                SetupDayPlanDishLinkRepositoryDeleteAll();
                SetupDayHistoryRepository();
                SetupPushHandler();

                // Act.
                var result = await _sut.UpsertDishAsync(
                    scenario.HouseholdId,
                    scenario.Date,
                    null,
                    null,
                    null,
                    0,
                    scenario.ActingHousemateId);

                // Assert.
                var deleteAllLinksCalled = false;
                try
                {
                    _dayPlanDishLinkRepositoryMock.Verify(
                        x => x.DeleteAllAsync(scenario.HouseholdId, scenario.Date, It.IsAny<CancellationToken>()),
                        Times.Once());
                    deleteAllLinksCalled = true;
                }
                catch { }

                var dishHandledCorrectly = false;

                if (scenario.ExistingDishRecord is null)
                {
                    // No existing record and no dinnerTime → returns Deleted without DB writes.
                    dishHandledCorrectly = result == DishUpsertResult.Deleted;
                }
                else if (scenario.ExistingDishRecord.DinnerTime is null)
                {
                    // Existing record with no DinnerTime → DeleteAsync called.
                    try
                    {
                        _dishRepositoryMock.Verify(
                            x => x.DeleteAsync(scenario.HouseholdId, scenario.Date, It.IsAny<CancellationToken>()),
                            Times.Once());
                        dishHandledCorrectly = result == DishUpsertResult.Deleted;
                    }
                    catch { }
                }
                else
                {
                    // Existing record with DinnerTime → UpsertAsync with empty description preserving DinnerTime.
                    try
                    {
                        _dishRepositoryMock.Verify(
                            x => x.UpsertAsync(
                                It.Is<DishRecord>(r =>
                                    r.HouseholdId == scenario.HouseholdId &&
                                    r.Date == scenario.Date &&
                                    r.Description == string.Empty &&
                                    r.DinnerTime == scenario.ExistingDishRecord.DinnerTime),
                                It.IsAny<CancellationToken>()),
                            Times.Once());
                        dishHandledCorrectly = result == DishUpsertResult.Deleted;
                    }
                    catch { }
                }

                return (deleteAllLinksCalled && dishHandledCorrectly)
                    .Label($"Result={result}, DeleteAllLinksCalled={deleteAllLinksCalled}, DishHandledCorrectly={dishHandledCorrectly}, " +
                           $"HasExisting={scenario.ExistingDishRecord is not null}, HasDinnerTime={scenario.ExistingDishRecord?.DinnerTime is not null}");
            });
    }

    private void ResetAllMocks()
    {
        _housemateRepositoryMock.Reset();
        _attendanceRepositoryMock.Reset();
        _dishRepositoryMock.Reset();
        _commentRepositoryMock.Reset();
        _dayHistoryRepositoryMock.Reset();
        _pushHandlerMock.Reset();
        _savedDishRepositoryMock.Reset();
        _dayPlanDishLinkRepositoryMock.Reset();
    }

    private void SetupSavedDishRepository(Guid householdId, IReadOnlyList<SavedDish> savedDishes)
    {
        _savedDishRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedDishes);
    }

    private void SetupDishRepositoryGet(Guid householdId, DateOnly date, DishRecord? existingRecord)
    {
        _dishRepositoryMock
            .Setup(x => x.GetAsync(householdId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRecord);
    }

    private void SetupDishRepositoryUpsert()
    {
        _dishRepositoryMock
            .Setup(x => x.UpsertAsync(It.IsAny<DishRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupDishRepositoryDelete()
    {
        _dishRepositoryMock
            .Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupDayPlanDishLinkRepositoryReplaceAll()
    {
        _dayPlanDishLinkRepositoryMock
            .Setup(x => x.ReplaceAllAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyList<DayPlanDishLink>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _dayPlanDishLinkRepositoryMock
            .Setup(x => x.GetByDateAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DayPlanDishLink>());
    }

    private void SetupDayPlanDishLinkRepositoryDeleteAll()
    {
        _dayPlanDishLinkRepositoryMock
            .Setup(x => x.DeleteAllAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupDayHistoryRepository()
    {
        _dayHistoryRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<DayHistoryEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupPushHandler()
    {
        _pushHandlerMock
            .Setup(x => x.SendAutoNotificationsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static Arbitrary<SavedModeScenario> SavedModeScenarioArb()
    {
        var householdIdGen = ArbMap.Default.GeneratorFor<Guid>();
        var actingHousemateIdGen = ArbMap.Default.GeneratorFor<Guid>();
        var dateGen = Gen.Choose(2024, 2026).SelectMany(year =>
            Gen.Choose(1, 12).SelectMany(month =>
                Gen.Choose(1, 28).Select(day => new DateOnly(year, month, day))));

        // Generate 1–10 unique saved dish IDs with corresponding SavedDish records.
        var savedDishCountGen = Gen.Choose(1, 10);
        var gen = householdIdGen.SelectMany(householdId =>
            actingHousemateIdGen.SelectMany(actingHousemateId =>
                dateGen.SelectMany(date =>
                    savedDishCountGen.SelectMany(count =>
                        Gen.ListOf(ArbMap.Default.GeneratorFor<Guid>(), count).Select(ids =>
                        {
                            // Ensure unique IDs.
                            var uniqueIds = ids.Distinct().Take(count).ToList();
                            if (uniqueIds.Count == 0)
                                uniqueIds = new List<Guid> { Guid.NewGuid() };

                            var savedDishes = uniqueIds
                                .Select(x => new SavedDish(x, householdId, $"Dish_{x.ToString()[..8]}", false))
                                .ToList();

                            return new SavedModeScenario(
                                householdId,
                                date,
                                actingHousemateId,
                                uniqueIds,
                                savedDishes,
                                null);
                        })))));

        return Arb.From(gen);
    }

    private static Arbitrary<CustomModeNoMatchScenario> CustomModeNoMatchScenarioArb()
    {
        var householdIdGen = ArbMap.Default.GeneratorFor<Guid>();
        var actingHousemateIdGen = ArbMap.Default.GeneratorFor<Guid>();
        var dateGen = Gen.Choose(2024, 2026).SelectMany(year =>
            Gen.Choose(1, 12).SelectMany(month =>
                Gen.Choose(1, 28).Select(day => new DateOnly(year, month, day))));

        // Generate a random description that will NOT match any saved dish.
        // Use a prefix that makes collision impossible.
        var printableCharGen = Gen.Choose(33, 126).Select(x => (char)x);
        var descriptionGen = Gen.Choose(1, 50)
            .SelectMany(length => Gen.ListOf(printableCharGen, length)
                .Select(chars => "NOMATCH_" + new string(chars.ToArray())));

        // Generate some saved dishes that will NOT match the description.
        var savedDishCountGen = Gen.Choose(0, 5);

        var gen = householdIdGen.SelectMany(householdId =>
            actingHousemateIdGen.SelectMany(actingHousemateId =>
                dateGen.SelectMany(date =>
                    descriptionGen.SelectMany(description =>
                        savedDishCountGen.SelectMany(dishCount =>
                            Gen.ListOf(ArbMap.Default.GeneratorFor<Guid>(), dishCount).Select(ids =>
                            {
                                var savedDishes = ids
                                    .Select(x => new SavedDish(x, householdId, $"SavedDish_{x.ToString()[..8]}", false))
                                    .ToList();

                                return new CustomModeNoMatchScenario(
                                    householdId,
                                    date,
                                    actingHousemateId,
                                    description,
                                    savedDishes);
                            }))))));

        return Arb.From(gen);
    }

    private static Arbitrary<EmptySaveScenario> EmptySaveScenarioArb()
    {
        var householdIdGen = ArbMap.Default.GeneratorFor<Guid>();
        var actingHousemateIdGen = ArbMap.Default.GeneratorFor<Guid>();
        var dateGen = Gen.Choose(2024, 2026).SelectMany(year =>
            Gen.Choose(1, 12).SelectMany(month =>
                Gen.Choose(1, 28).Select(day => new DateOnly(year, month, day))));

        // Three scenarios: no existing record, existing with no DinnerTime, existing with DinnerTime.
        var scenarioTypeGen = Gen.Choose(0, 2);

        var gen = householdIdGen.SelectMany(householdId =>
            actingHousemateIdGen.SelectMany(actingHousemateId =>
                dateGen.SelectMany(date =>
                    scenarioTypeGen.SelectMany(scenarioType =>
                        Gen.Choose(6, 22).Select(hour =>
                        {
                            DishRecord? existingDishRecord = scenarioType switch
                            {
                                0 => null,
                                1 => new DishRecord(householdId, date, "SomeExistingDish", actingHousemateId, DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow),
                                _ => new DishRecord(householdId, date, "ExistingWithTime", actingHousemateId, DateTimeOffset.UtcNow, new TimeOnly(hour, 0), DateTimeOffset.UtcNow),
                            };

                            return new EmptySaveScenario(
                                householdId,
                                date,
                                actingHousemateId,
                                existingDishRecord);
                        })))));

        return Arb.From(gen);
    }

    private record SavedModeScenario(
        Guid HouseholdId,
        DateOnly Date,
        Guid ActingHousemateId,
        List<Guid> SavedDishIds,
        List<SavedDish> SavedDishes,
        DishRecord? ExistingDishRecord);

    private record CustomModeNoMatchScenario(
        Guid HouseholdId,
        DateOnly Date,
        Guid ActingHousemateId,
        string Description,
        List<SavedDish> SavedDishes);

    private record EmptySaveScenario(
        Guid HouseholdId,
        DateOnly Date,
        Guid ActingHousemateId,
        DishRecord? ExistingDishRecord);
}
