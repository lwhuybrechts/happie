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
    private readonly Mock<IDayPlanDishLinkRepository> _dayPlanDishLinkRepositoryMock = new();
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
            _savedDishRepositoryMock.Object,
            _dayPlanDishLinkRepositoryMock.Object);
    }

    /// <summary>
    /// For any DishRecord, GetDayPlanAsync returns the DishRecord's own description directly.
    /// Description resolution from saved dishes will be handled via DayPlanDishLinks in later tasks.
    /// Validates: Requirements 2.2, 2.3, 2.5, 2.6
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetDayPlanAsync_DescriptionResolution_ReturnsDishRecordDescription()
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

                // Act.
                var result = await _sut.GetDayPlanAsync(scenario.HouseholdId, scenario.Date);

                // Assert — description is returned directly from DishRecord.
                return (result.Dish!.Description == scenario.DishRecord!.Description)
                    .Label($"Expected '{scenario.DishRecord.Description}' but got '{result.Dish.Description}'");
            });
    }

    private static Arbitrary<DescriptionResolutionScenario> DescriptionResolutionScenarioArb()
    {
        var householdIdGen = ArbMap.Default.GeneratorFor<Guid>();
        var dateGen = Gen.Choose(2020, 2030).SelectMany(year =>
            Gen.Choose(1, 12).SelectMany(month =>
                Gen.Choose(1, 28).Select(day => new DateOnly(year, month, day))));
        var printableCharGen = Gen.Choose(33, 126).Select(x => (char)x);
        var descriptionGen = Gen.Choose(1, 50)
            .SelectMany(length => Gen.ListOf(printableCharGen, length)
                .Select(chars => new string(chars.ToArray())));

        var gen = householdIdGen.SelectMany(householdId =>
            dateGen.SelectMany(date =>
                descriptionGen.Select(description =>
                    new DescriptionResolutionScenario(
                        householdId,
                        date,
                        new DishRecord(householdId, date, description, Guid.NewGuid(), DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow)))));

        return Arb.From(gen);
    }

    private record DescriptionResolutionScenario(
        Guid HouseholdId,
        DateOnly Date,
        DishRecord? DishRecord);
}
