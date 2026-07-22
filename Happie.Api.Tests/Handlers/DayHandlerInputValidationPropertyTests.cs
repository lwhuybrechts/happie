using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Domain;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Results;
using Moq;

namespace Happie.Api.Tests.Handlers;

// Feature: happie, Property 4: Input validation rejects invalid dish save requests
/// <summary>
/// Property-based tests for <see cref="DayHandler.UpsertDishAsync"/> input validation.
/// Validates: Requirements 3.4, 5.4, 5.6, 10.5
/// </summary>
public class DayHandlerInputValidationPropertyTests
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

    public DayHandlerInputValidationPropertyTests()
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
    /// For any list of more than 10 savedDishIds (even if all IDs are valid),
    /// UpsertDishAsync SHALL return ValidationError.
    /// Validates: Requirements 3.4, 5.4, 5.6, 10.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_MoreThan10SavedDishIds_ReturnsValidationError()
    {
        return Prop.ForAll(
            TooManySavedDishIdsArb(),
            async scenario =>
            {
                // Reset mocks.
                ResetAllMocks();

                SetupSavedDishRepository(scenario.HouseholdId, scenario.SavedDishes);

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
                return (result == DishUpsertResult.ValidationError)
                    .Label($"Expected ValidationError but got {result} for {scenario.SavedDishIds.Count} savedDishIds");
            });
    }

    /// <summary>
    /// For any list of savedDishIds containing at least one duplicate GUID,
    /// UpsertDishAsync SHALL return ValidationError.
    /// Validates: Requirements 3.4, 5.4, 5.6, 10.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_DuplicateSavedDishIds_ReturnsValidationError()
    {
        return Prop.ForAll(
            DuplicateSavedDishIdsArb(),
            async scenario =>
            {
                // Reset mocks.
                ResetAllMocks();

                SetupSavedDishRepository(scenario.HouseholdId, scenario.SavedDishes);

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
                return (result == DishUpsertResult.ValidationError)
                    .Label($"Expected ValidationError but got {result} for list with duplicates (count={scenario.SavedDishIds.Count})");
            });
    }

    /// <summary>
    /// For any list of savedDishIds where at least one ID does not exist in the household's saved dishes,
    /// UpsertDishAsync SHALL return SavedDishNotFound.
    /// Validates: Requirements 3.4, 5.4, 5.6, 10.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_NonExistentSavedDishId_ReturnsSavedDishNotFound()
    {
        return Prop.ForAll(
            NonExistentSavedDishIdArb(),
            async scenario =>
            {
                // Reset mocks.
                ResetAllMocks();

                SetupSavedDishRepository(scenario.HouseholdId, scenario.SavedDishes);

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
                return (result == DishUpsertResult.SavedDishNotFound)
                    .Label($"Expected SavedDishNotFound but got {result}");
            });
    }

    /// <summary>
    /// For any request with both a non-empty savedDishIds list AND a non-empty description,
    /// UpsertDishAsync SHALL return ValidationError (mutual exclusion).
    /// Validates: Requirements 3.4, 5.4, 5.6, 10.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_BothSavedDishIdsAndDescription_ReturnsValidationError()
    {
        return Prop.ForAll(
            MutualExclusionArb(),
            async scenario =>
            {
                // Reset mocks.
                ResetAllMocks();

                // Act.
                var result = await _sut.UpsertDishAsync(
                    scenario.HouseholdId,
                    scenario.Date,
                    scenario.Description,
                    scenario.SavedDishIds,
                    null,
                    0,
                    scenario.ActingHousemateId);

                // Assert.
                return (result == DishUpsertResult.ValidationError)
                    .Label($"Expected ValidationError but got {result} for savedDishIds.Count={scenario.SavedDishIds.Count} and description='{scenario.Description}'");
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

    private static Gen<DateOnly> DateGen()
    {
        return Gen.Choose(2024, 2026).SelectMany(year =>
            Gen.Choose(1, 12).SelectMany(month =>
                Gen.Choose(1, 28).Select(day => new DateOnly(year, month, day))));
    }

    private static Arbitrary<InputValidationScenario> TooManySavedDishIdsArb()
    {
        var gen = ArbMap.Default.GeneratorFor<Guid>().SelectMany(householdId =>
            ArbMap.Default.GeneratorFor<Guid>().SelectMany(actingHousemateId =>
                DateGen().SelectMany(date =>
                    Gen.Choose(11, 20).SelectMany(count =>
                        Gen.ListOf(ArbMap.Default.GeneratorFor<Guid>(), count).Select(ids =>
                        {
                            // Ensure unique IDs.
                            var uniqueIds = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();
                            var savedDishes = uniqueIds
                                .Select(x => new SavedDish(x, householdId, $"Dish {x}", false))
                                .ToList();

                            return new InputValidationScenario(
                                householdId,
                                date,
                                actingHousemateId,
                                uniqueIds,
                                savedDishes,
                                null);
                        })))));

        return Arb.From(gen);
    }

    private static Arbitrary<InputValidationScenario> DuplicateSavedDishIdsArb()
    {
        var gen = ArbMap.Default.GeneratorFor<Guid>().SelectMany(householdId =>
            ArbMap.Default.GeneratorFor<Guid>().SelectMany(actingHousemateId =>
                DateGen().SelectMany(date =>
                    Gen.Choose(2, 10).SelectMany(count =>
                    {
                        // Generate unique IDs, then inject a duplicate.
                        var uniqueCount = count - 1;
                        return Gen.ListOf(ArbMap.Default.GeneratorFor<Guid>(), uniqueCount).SelectMany(baseIds =>
                            Gen.Choose(0, uniqueCount - 1).Select(duplicateIndex =>
                            {
                                var uniqueIds = Enumerable.Range(0, uniqueCount).Select(_ => Guid.NewGuid()).ToList();
                                // Add a duplicate of one of the IDs.
                                var idsWithDuplicate = new List<Guid>(uniqueIds) { uniqueIds[duplicateIndex] };
                                var savedDishes = uniqueIds
                                    .Select(x => new SavedDish(x, householdId, $"Dish {x}", false))
                                    .ToList();

                                return new InputValidationScenario(
                                    householdId,
                                    date,
                                    actingHousemateId,
                                    idsWithDuplicate,
                                    savedDishes,
                                    null);
                            }));
                    }))));

        return Arb.From(gen);
    }

    private static Arbitrary<InputValidationScenario> NonExistentSavedDishIdArb()
    {
        var gen = ArbMap.Default.GeneratorFor<Guid>().SelectMany(householdId =>
            ArbMap.Default.GeneratorFor<Guid>().SelectMany(actingHousemateId =>
                DateGen().SelectMany(date =>
                    Gen.Choose(1, 9).SelectMany(validCount =>
                    {
                        // Generate valid IDs that exist in the repository.
                        return Gen.Choose(0, validCount - 1).Select(insertIndex =>
                        {
                            var validIds = Enumerable.Range(0, validCount).Select(_ => Guid.NewGuid()).ToList();
                            var nonExistentId = Guid.NewGuid();

                            // Insert the non-existent ID at a random position.
                            var allIds = new List<Guid>(validIds);
                            allIds.Insert(insertIndex, nonExistentId);

                            var savedDishes = validIds
                                .Select(x => new SavedDish(x, householdId, $"Dish {x}", false))
                                .ToList();

                            return new InputValidationScenario(
                                householdId,
                                date,
                                actingHousemateId,
                                allIds,
                                savedDishes,
                                null);
                        });
                    }))));

        return Arb.From(gen);
    }

    private static Arbitrary<InputValidationScenario> MutualExclusionArb()
    {
        var printableCharGen = Gen.Choose(33, 126).Select(x => (char)x);
        var nonEmptyDescriptionGen = Gen.Choose(1, 100)
            .SelectMany(length => Gen.ListOf(printableCharGen, length)
                .Select(chars => new string(chars.ToArray())));

        var gen = ArbMap.Default.GeneratorFor<Guid>().SelectMany(householdId =>
            ArbMap.Default.GeneratorFor<Guid>().SelectMany(actingHousemateId =>
                DateGen().SelectMany(date =>
                    Gen.Choose(1, 10).SelectMany(count =>
                        nonEmptyDescriptionGen.Select(description =>
                        {
                            var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

                            return new InputValidationScenario(
                                householdId,
                                date,
                                actingHousemateId,
                                ids,
                                new List<SavedDish>(),
                                description);
                        })))));

        return Arb.From(gen);
    }

    private record InputValidationScenario(
        Guid HouseholdId,
        DateOnly Date,
        Guid ActingHousemateId,
        List<Guid> SavedDishIds,
        List<SavedDish> SavedDishes,
        string? Description);
}
