using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Domain;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Results;
using Moq;

namespace Happie.Api.Tests.Handlers;

// Feature: saved-dishes, Property 8: Dish save mutual exclusion
/// <summary>
/// Property-based tests for <see cref="DayHandler.UpsertDishAsync"/> basic behavior.
/// Mutual exclusion between savedDishIds and description will be tested in later tasks (4.x)
/// once the multi-dish signature is introduced.
/// Validates: Requirements 9.6, 9.7
/// </summary>
public class DayHandlerDishSaveMutualExclusionPropertyTests
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

    /// <summary>Initializes a new instance of <see cref="DayHandlerDishSaveMutualExclusionPropertyTests"/>.</summary>
    public DayHandlerDishSaveMutualExclusionPropertyTests()
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
    /// For any dish save request with null/empty description, the dish should be deleted.
    /// For any dish save request with a non-empty description, the dish should be saved successfully.
    /// Full mutual exclusion with savedDishIds will be tested after task 4.1 introduces the new signature.
    /// Validates: Requirements 9.6, 9.7
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_NullOrEmptyDescription_DeletesDish()
    {
        return Prop.ForAll(
            DeleteScenarioArb(),
            async scenario =>
            {
                // Reset mocks.
                _dishRepositoryMock.Reset();
                _dayHistoryRepositoryMock.Reset();

                _dishRepositoryMock
                    .Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((DishRecord?)null);

                _dishRepositoryMock
                    .Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                _dayHistoryRepositoryMock
                    .Setup(x => x.AddAsync(It.IsAny<DayHistoryEntry>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

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
                return (result == DishUpsertResult.Deleted)
                    .Label($"Expected Deleted but got {result} for Description='{scenario.Description}'");
            });
    }

    /// <summary>
    /// For any dish save request with a non-empty description, the dish should be saved successfully.
    /// Validates: Requirements 9.6, 9.7
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_NonEmptyDescription_ReturnsSuccess()
    {
        return Prop.ForAll(
            SuccessScenarioArb(),
            async scenario =>
            {
                // Reset mocks.
                _dishRepositoryMock.Reset();
                _dayHistoryRepositoryMock.Reset();
                _pushHandlerMock.Reset();

                _dishRepositoryMock
                    .Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((DishRecord?)null);

                _dishRepositoryMock
                    .Setup(x => x.UpsertAsync(It.IsAny<DishRecord>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                _dayHistoryRepositoryMock
                    .Setup(x => x.AddAsync(It.IsAny<DayHistoryEntry>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                _pushHandlerMock
                    .Setup(x => x.SendAutoNotificationsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

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
                return (result == DishUpsertResult.Success)
                    .Label($"Expected Success but got {result} for Description='{scenario.Description}'");
            });
    }

    private static Arbitrary<BasicDishScenario> DeleteScenarioArb()
    {
        var householdIdGen = ArbMap.Default.GeneratorFor<Guid>();
        var actingHousemateIdGen = ArbMap.Default.GeneratorFor<Guid>();
        var dateGen = Gen.Choose(2024, 2026).SelectMany(year =>
            Gen.Choose(1, 12).SelectMany(month =>
                Gen.Choose(1, 28).Select(day => new DateOnly(year, month, day))));

        var nullOrEmptyDescriptionGen = Gen.Elements<string?>(null, "");

        var gen = householdIdGen.SelectMany(householdId =>
            actingHousemateIdGen.SelectMany(actingHousemateId =>
                dateGen.SelectMany(date =>
                    nullOrEmptyDescriptionGen.Select(description =>
                        new BasicDishScenario(householdId, date, description, actingHousemateId)))));

        return Arb.From(gen);
    }

    private static Arbitrary<BasicDishScenario> SuccessScenarioArb()
    {
        var householdIdGen = ArbMap.Default.GeneratorFor<Guid>();
        var actingHousemateIdGen = ArbMap.Default.GeneratorFor<Guid>();
        var dateGen = Gen.Choose(2024, 2026).SelectMany(year =>
            Gen.Choose(1, 12).SelectMany(month =>
                Gen.Choose(1, 28).Select(day => new DateOnly(year, month, day))));

        var printableCharGen = Gen.Choose(33, 126).Select(x => (char)x);
        var nonEmptyDescriptionGen = Gen.Choose(1, 100)
            .SelectMany(length => Gen.ListOf(printableCharGen, length)
                .Select(chars => new string(chars.ToArray())));

        var gen = householdIdGen.SelectMany(householdId =>
            actingHousemateIdGen.SelectMany(actingHousemateId =>
                dateGen.SelectMany(date =>
                    nonEmptyDescriptionGen.Select(description =>
                        new BasicDishScenario(householdId, date, description, actingHousemateId)))));

        return Arb.From(gen);
    }

    private record BasicDishScenario(
        Guid HouseholdId,
        DateOnly Date,
        string? Description,
        Guid ActingHousemateId);
}
