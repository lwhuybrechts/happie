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
/// <summary>Property-based tests for <see cref="DayHandler.UpsertDishAsync"/> mutual exclusion validation.</summary>
public class DayHandlerDishSaveMutualExclusionPropertyTests
{
    private readonly Mock<IHousemateRepository> _housemateRepositoryMock = new();
    private readonly Mock<IAttendanceRepository> _attendanceRepositoryMock = new();
    private readonly Mock<IDishRepository> _dishRepositoryMock = new();
    private readonly Mock<ICommentRepository> _commentRepositoryMock = new();
    private readonly Mock<IDayHistoryRepository> _dayHistoryRepositoryMock = new();
    private readonly Mock<IPushHandler> _pushHandlerMock = new();
    private readonly Mock<ISavedDishRepository> _savedDishRepositoryMock = new();
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
            _savedDishRepositoryMock.Object);
    }

    /// <summary>
    /// For any dish save request, if both SavedDishId is non-null and Description is non-null
    /// and non-empty, the request should be rejected with a validation error. If both are
    /// null/empty, the DishRecord should be deleted. If only SavedDishId is set and exists,
    /// returns Success. If only SavedDishId is set but doesn't exist, returns SavedDishNotFound.
    /// If only Description is set, returns Success.
    /// Validates: Requirements 9.6, 9.7
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_MutualExclusion_IsEnforced()
    {
        return Prop.ForAll(
            MutualExclusionScenarioArb(),
            async scenario =>
            {
                // Reset mocks.
                _housemateRepositoryMock.Reset();
                _attendanceRepositoryMock.Reset();
                _dishRepositoryMock.Reset();
                _commentRepositoryMock.Reset();
                _dayHistoryRepositoryMock.Reset();
                _pushHandlerMock.Reset();
                _savedDishRepositoryMock.Reset();

                // Setup mocks.
                _dishRepositoryMock
                    .Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((DishRecord?)null);

                _dishRepositoryMock
                    .Setup(x => x.UpsertAsync(It.IsAny<DishRecord>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                _dishRepositoryMock
                    .Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                _savedDishRepositoryMock
                    .Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(scenario.SavedDishExists
                        ? new SavedDish(scenario.SavedDishId ?? Guid.NewGuid(), scenario.HouseholdId, "Test Dish", false)
                        : null);

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
                    scenario.SavedDishId,
                    null,
                    0,
                    scenario.ActingHousemateId);

                // Assert.
                return scenario.ExpectedResult switch
                {
                    DishUpsertResult.ValidationError =>
                        (result == DishUpsertResult.ValidationError)
                            .Label($"Expected ValidationError but got {result} for SavedDishId={scenario.SavedDishId}, Description='{scenario.Description}'"),

                    DishUpsertResult.Deleted =>
                        (result == DishUpsertResult.Deleted)
                            .Label($"Expected Deleted but got {result} for SavedDishId={scenario.SavedDishId}, Description='{scenario.Description}'"),

                    DishUpsertResult.Success =>
                        (result == DishUpsertResult.Success)
                            .Label($"Expected Success but got {result} for SavedDishId={scenario.SavedDishId}, Description='{scenario.Description}'"),

                    DishUpsertResult.SavedDishNotFound =>
                        (result == DishUpsertResult.SavedDishNotFound)
                            .Label($"Expected SavedDishNotFound but got {result} for SavedDishId={scenario.SavedDishId}, Description='{scenario.Description}'"),

                    _ => throw new InvalidOperationException($"Unhandled {nameof(DishUpsertResult)}: {scenario.ExpectedResult}")
                };
            });
    }

    private static Arbitrary<MutualExclusionScenario> MutualExclusionScenarioArb()
    {
        var householdIdGen = ArbMap.Default.GeneratorFor<Guid>();
        var actingHousemateIdGen = ArbMap.Default.GeneratorFor<Guid>();
        var dateGen = Gen.Choose(2024, 2026).SelectMany(year =>
            Gen.Choose(1, 12).SelectMany(month =>
                Gen.Choose(1, 28).Select(day => new DateOnly(year, month, day))));

        var printableCharGen = Gen.Choose(33, 126).Select(x => (char)x);

        // Generate non-empty descriptions (1-100 chars).
        var nonEmptyDescriptionGen = Gen.Choose(1, 100)
            .SelectMany(length => Gen.ListOf(printableCharGen, length)
                .Select(chars => new string(chars.ToArray())));

        // Generate null or empty descriptions (matching string.IsNullOrEmpty semantics).
        var nullOrEmptyDescriptionGen = Gen.Elements<string?>(null, "");

        var guidGen = ArbMap.Default.GeneratorFor<Guid>();

        // Scenario type: 0=both set, 1=both null/empty, 2=only savedDishId (exists), 3=only savedDishId (not exists), 4=only description.
        var gen = Gen.Choose(0, 4).SelectMany(scenarioType =>
            householdIdGen.SelectMany(householdId =>
                actingHousemateIdGen.SelectMany(actingHousemateId =>
                    dateGen.SelectMany(date =>
                    {
                        return scenarioType switch
                        {
                            // Both SavedDishId and Description are non-null/non-empty → ValidationError.
                            0 => guidGen.SelectMany(savedDishId =>
                                nonEmptyDescriptionGen.Select(description =>
                                    new MutualExclusionScenario(
                                        householdId,
                                        date,
                                        description,
                                        savedDishId,
                                        false,
                                        actingHousemateId,
                                        DishUpsertResult.ValidationError))),

                            // Both null/empty → Deleted.
                            1 => nullOrEmptyDescriptionGen.Select(description =>
                                new MutualExclusionScenario(
                                    householdId,
                                    date,
                                    description,
                                    null,
                                    false,
                                    actingHousemateId,
                                    DishUpsertResult.Deleted)),

                            // Only SavedDishId set (description null/empty), saved dish exists → Success.
                            2 => guidGen.SelectMany(savedDishId =>
                                nullOrEmptyDescriptionGen.Select(description =>
                                    new MutualExclusionScenario(
                                        householdId,
                                        date,
                                        description,
                                        savedDishId,
                                        true,
                                        actingHousemateId,
                                        DishUpsertResult.Success))),

                            // Only SavedDishId set (description null/empty), saved dish doesn't exist → SavedDishNotFound.
                            3 => guidGen.SelectMany(savedDishId =>
                                nullOrEmptyDescriptionGen.Select(description =>
                                    new MutualExclusionScenario(
                                        householdId,
                                        date,
                                        description,
                                        savedDishId,
                                        false,
                                        actingHousemateId,
                                        DishUpsertResult.SavedDishNotFound))),

                            // Only Description set (savedDishId null) → Success.
                            _ => nonEmptyDescriptionGen.Select(description =>
                                new MutualExclusionScenario(
                                    householdId,
                                    date,
                                    description,
                                    null,
                                    false,
                                    actingHousemateId,
                                    DishUpsertResult.Success))
                        };
                    }))));

        return Arb.From(gen);
    }

    private record MutualExclusionScenario(
        Guid HouseholdId,
        DateOnly Date,
        string? Description,
        Guid? SavedDishId,
        bool SavedDishExists,
        Guid ActingHousemateId,
        DishUpsertResult ExpectedResult);
}
