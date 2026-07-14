using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Domain;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Shared.Domain;
using Moq;

namespace Happie.Api.Tests.Handlers;

// Feature: saved-dishes, Property 2: Description resolution correctness
/// <summary>Property-based tests for <see cref="DayHandler.GetDayPlanAsync"/> description resolution behavior.</summary>
public class DayHandlerDescriptionResolutionPropertyTests
{
    private readonly Mock<IHousemateRepository> _housemateRepositoryMock = new();
    private readonly Mock<IAttendanceRepository> _attendanceRepositoryMock = new();
    private readonly Mock<IDishRepository> _dishRepositoryMock = new();
    private readonly Mock<ICommentRepository> _commentRepositoryMock = new();
    private readonly Mock<IDayHistoryRepository> _dayHistoryRepositoryMock = new();
    private readonly Mock<IPushHandler> _pushHandlerMock = new();
    private readonly Mock<ISavedDishRepository> _savedDishRepositoryMock = new();
    private readonly DayHandler _sut;

    /// <summary>Initializes a new instance of <see cref="DayHandlerDescriptionResolutionPropertyTests"/>.</summary>
    public DayHandlerDescriptionResolutionPropertyTests()
    {
        _sut = new DayHandler(
            _housemateRepositoryMock.Object,
            _attendanceRepositoryMock.Object,
            _dishRepositoryMock.Object,
            _commentRepositoryMock.Object,
            _dayHistoryRepositoryMock.Object,
            _pushHandlerMock.Object,
            _savedDishRepositoryMock.Object);
    }

    /// <summary>
    /// For any DishRecord with a SavedDishId and a corresponding set of SavedDish records for the household,
    /// the resolved description should equal the SavedDish's description when the referenced SavedDish exists
    /// (active or soft-deleted), and should fall back to the DishRecord's own description when the referenced
    /// SavedDish does not exist. When SavedDishId is null, the DishRecord's own description is always used.
    /// Validates: Requirements 2.2, 2.3, 2.5, 2.6
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetDayPlanAsync_DescriptionResolution_CorrectlyResolvesFromSavedDish()
    {
        return Prop.ForAll(
            DescriptionResolutionScenarioArb(),
            async scenario =>
            {
                // Arrange.
                _housemateRepositoryMock.Reset();
                _attendanceRepositoryMock.Reset();
                _dishRepositoryMock.Reset();
                _commentRepositoryMock.Reset();
                _dayHistoryRepositoryMock.Reset();
                _pushHandlerMock.Reset();
                _savedDishRepositoryMock.Reset();

                var housemateId = Guid.NewGuid();
                var housemate = new Housemate(housemateId, scenario.HouseholdId, "TestHousemate", "#E91E63", false);

                _housemateRepositoryMock
                    .Setup(x => x.GetAllAsync(scenario.HouseholdId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<Housemate> { housemate });

                _attendanceRepositoryMock
                    .Setup(x => x.GetByDateAsync(scenario.HouseholdId, scenario.Date, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<AttendanceRecord>());

                _dishRepositoryMock
                    .Setup(x => x.GetAsync(scenario.HouseholdId, scenario.Date, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(scenario.DishRecord);

                _commentRepositoryMock
                    .Setup(x => x.GetByDateAsync(scenario.HouseholdId, scenario.Date, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<Comment>());

                _dayHistoryRepositoryMock
                    .Setup(x => x.GetByDateAsync(scenario.HouseholdId, scenario.Date, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<DayHistoryEntry>());

                if (scenario.SavedDish is not null)
                {
                    _savedDishRepositoryMock
                        .Setup(x => x.GetAsync(scenario.HouseholdId, scenario.SavedDish.Id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(scenario.SavedDish);
                }
                else if (scenario.DishRecord?.SavedDishId is not null)
                {
                    // Orphaned reference: saved dish not found.
                    _savedDishRepositoryMock
                        .Setup(x => x.GetAsync(scenario.HouseholdId, scenario.DishRecord.SavedDishId.Value, It.IsAny<CancellationToken>()))
                        .ReturnsAsync((SavedDish?)null);
                }

                // Act.
                var result = await _sut.GetDayPlanAsync(scenario.HouseholdId, scenario.Date);

                // Assert.
                return scenario.ScenarioType switch
                {
                    DescriptionScenarioType.NullSavedDishId =>
                        (result.Dish!.Description == scenario.DishRecord!.Description)
                            .Label($"Null SavedDishId: expected '{scenario.DishRecord.Description}' but got '{result.Dish.Description}'")
                            .And((result.Dish.SavedDishId == null)
                                .Label("Null SavedDishId: response SavedDishId should be null")),

                    DescriptionScenarioType.ActiveSavedDishExists =>
                        (result.Dish!.Description == scenario.SavedDish!.Description)
                            .Label($"Active SavedDish: expected '{scenario.SavedDish.Description}' but got '{result.Dish.Description}'")
                            .And((result.Dish.SavedDishId == scenario.SavedDish.Id)
                                .Label($"Active SavedDish: SavedDishId should be {scenario.SavedDish.Id} but got {result.Dish.SavedDishId}")),

                    DescriptionScenarioType.SoftDeletedSavedDishExists =>
                        (result.Dish!.Description == scenario.SavedDish!.Description)
                            .Label($"Soft-deleted SavedDish: expected '{scenario.SavedDish.Description}' but got '{result.Dish.Description}'")
                            .And((result.Dish.SavedDishId == scenario.SavedDish.Id)
                                .Label($"Soft-deleted SavedDish: SavedDishId should be {scenario.SavedDish.Id} but got {result.Dish.SavedDishId}")),

                    DescriptionScenarioType.OrphanedSavedDishId =>
                        (result.Dish!.Description == scenario.DishRecord!.Description)
                            .Label($"Orphaned: expected '{scenario.DishRecord.Description}' but got '{result.Dish.Description}'")
                            .And((result.Dish.SavedDishId == null)
                                .Label("Orphaned: response SavedDishId should be null")),

                    _ => throw new InvalidOperationException($"Unhandled {nameof(DescriptionScenarioType)}: {scenario.ScenarioType}")
                };
            });
    }

    private static Arbitrary<DescriptionResolutionScenario> DescriptionResolutionScenarioArb()
    {
        var gen = Gen.Choose(0, 3).SelectMany(scenarioType =>
        {
            var householdIdGen = ArbMap.Default.GeneratorFor<Guid>();
            var dateGen = Gen.Choose(2020, 2030).SelectMany(year =>
                Gen.Choose(1, 12).SelectMany(month =>
                    Gen.Choose(1, 28).Select(day => new DateOnly(year, month, day))));
            var printableCharGen = Gen.Choose(33, 126).Select(x => (char)x);
            var descriptionGen = Gen.Choose(1, 50)
                .SelectMany(length => Gen.ListOf(printableCharGen, length)
                    .Select(chars => new string(chars.ToArray())));

            return scenarioType switch
            {
                // Scenario 0: DishRecord with null SavedDishId → uses own description.
                0 => householdIdGen.SelectMany(householdId =>
                    dateGen.SelectMany(date =>
                        descriptionGen.Select(description =>
                            new DescriptionResolutionScenario(
                                householdId,
                                date,
                                new DishRecord(householdId, date, description, Guid.NewGuid(), DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow, null),
                                null,
                                DescriptionScenarioType.NullSavedDishId)))),

                // Scenario 1: DishRecord with SavedDishId that matches an active SavedDish → uses SavedDish.Description.
                1 => householdIdGen.SelectMany(householdId =>
                    dateGen.SelectMany(date =>
                        descriptionGen.SelectMany(dishDescription =>
                            descriptionGen.SelectMany(savedDishDescription =>
                                ArbMap.Default.GeneratorFor<Guid>().Select(savedDishId =>
                                    new DescriptionResolutionScenario(
                                        householdId,
                                        date,
                                        new DishRecord(householdId, date, dishDescription, Guid.NewGuid(), DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow, savedDishId),
                                        new SavedDish(savedDishId, householdId, savedDishDescription, false),
                                        DescriptionScenarioType.ActiveSavedDishExists)))))),

                // Scenario 2: DishRecord with SavedDishId that matches a soft-deleted SavedDish → still uses SavedDish.Description.
                2 => householdIdGen.SelectMany(householdId =>
                    dateGen.SelectMany(date =>
                        descriptionGen.SelectMany(dishDescription =>
                            descriptionGen.SelectMany(savedDishDescription =>
                                ArbMap.Default.GeneratorFor<Guid>().Select(savedDishId =>
                                    new DescriptionResolutionScenario(
                                        householdId,
                                        date,
                                        new DishRecord(householdId, date, dishDescription, Guid.NewGuid(), DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow, savedDishId),
                                        new SavedDish(savedDishId, householdId, savedDishDescription, true),
                                        DescriptionScenarioType.SoftDeletedSavedDishExists)))))),

                // Scenario 3: DishRecord with SavedDishId that doesn't match any SavedDish → falls back to own description.
                _ => householdIdGen.SelectMany(householdId =>
                    dateGen.SelectMany(date =>
                        descriptionGen.SelectMany(description =>
                            ArbMap.Default.GeneratorFor<Guid>().Select(orphanedSavedDishId =>
                                new DescriptionResolutionScenario(
                                    householdId,
                                    date,
                                    new DishRecord(householdId, date, description, Guid.NewGuid(), DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow, orphanedSavedDishId),
                                    null,
                                    DescriptionScenarioType.OrphanedSavedDishId)))))
            };
        });

        return Arb.From(gen);
    }

    private enum DescriptionScenarioType
    {
        NullSavedDishId,
        ActiveSavedDishExists,
        SoftDeletedSavedDishExists,
        OrphanedSavedDishId
    }

    private record DescriptionResolutionScenario(
        Guid HouseholdId,
        DateOnly Date,
        DishRecord? DishRecord,
        SavedDish? SavedDish,
        DescriptionScenarioType ScenarioType);
}
