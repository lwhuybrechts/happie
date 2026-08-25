using Happie.Api.Domain;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Shared.Domain;
using Moq;

namespace Happie.Api.Tests.Handlers;

/// <summary>Unit tests for <see cref="DishStatisticsHandler"/>.</summary>
public class DishStatisticsHandlerTests
{
    private readonly Mock<IAttendanceRepository> _attendanceRepositoryMock = new();
    private readonly Mock<IDayPlanDishLinkRepository> _dayPlanDishLinkRepositoryMock = new();
    private readonly Mock<ISavedDishRepository> _savedDishRepositoryMock = new();
    private readonly Mock<IHousemateRepository> _housemateRepositoryMock = new();
    private readonly DishStatisticsHandler _sut;

    public DishStatisticsHandlerTests()
    {
        _sut = new DishStatisticsHandler(
            _attendanceRepositoryMock.Object,
            _dayPlanDishLinkRepositoryMock.Object,
            _savedDishRepositoryMock.Object,
            _housemateRepositoryMock.Object);
    }

    /// <summary>Future dish links are excluded from all-time times cooked.</summary>
    [Fact]
    public async Task GetStatisticsAsync_FutureDishLink_ExcludedFromAllTimeCount()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var pastDate = today.AddDays(-5);
        var futureDate = today.AddDays(1);

        SetupRepositories(householdId, savedDishId, new List<DayPlanDishLink>
        {
            new(householdId, pastDate, savedDishId, 0),
            new(householdId, futureDate, savedDishId, 0)
        });

        // Act.
        var result = await _sut.GetStatisticsAsync(
            householdId, savedDishId, today.AddDays(-30), today);

        // Assert.
        Assert.Equal(1, result.AllTimeTimesCooked);
    }

    /// <summary>Future dish links are excluded from last cooked date.</summary>
    [Fact]
    public async Task GetStatisticsAsync_FutureDishLink_ExcludedFromLastCookedDate()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var pastDate = today.AddDays(-5);
        var futureDate = today.AddDays(1);

        SetupRepositories(householdId, savedDishId, new List<DayPlanDishLink>
        {
            new(householdId, pastDate, savedDishId, 0),
            new(householdId, futureDate, savedDishId, 0)
        });

        // Act.
        var result = await _sut.GetStatisticsAsync(
            householdId, savedDishId, today.AddDays(-30), today);

        // Assert.
        Assert.Equal(pastDate, result.LastCookedDate);
    }

    /// <summary>Future dish links are excluded from the in-range times cooked count.</summary>
    [Fact]
    public async Task GetStatisticsAsync_FutureDishLinkInRange_ExcludedFromTimesCooked()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var pastDate = today.AddDays(-3);
        var futureDate = today.AddDays(1);

        // Range includes both past and future.
        SetupRepositories(householdId, savedDishId, new List<DayPlanDishLink>
        {
            new(householdId, pastDate, savedDishId, 0),
            new(householdId, futureDate, savedDishId, 0)
        });

        // Act.
        var result = await _sut.GetStatisticsAsync(
            householdId, savedDishId, today.AddDays(-30), today.AddDays(7));

        // Assert.
        Assert.Equal(1, result.TimesCooked);
    }

    /// <summary>A dish cooked today is included in the counts.</summary>
    [Fact]
    public async Task GetStatisticsAsync_DishCookedToday_IncludedInCounts()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        SetupRepositories(householdId, savedDishId, new List<DayPlanDishLink>
        {
            new(householdId, today, savedDishId, 0)
        });

        // Act.
        var result = await _sut.GetStatisticsAsync(
            householdId, savedDishId, today.AddDays(-30), today);

        // Assert.
        Assert.Equal(1, result.AllTimeTimesCooked);
        Assert.Equal(today, result.LastCookedDate);
    }

    /// <summary>When all dish links are in the future, all-time count is zero and last cooked is null.</summary>
    [Fact]
    public async Task GetStatisticsAsync_OnlyFutureLinks_ReturnsZeroAndNullLastCooked()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var futureDate1 = today.AddDays(1);
        var futureDate2 = today.AddDays(3);

        SetupRepositories(householdId, savedDishId, new List<DayPlanDishLink>
        {
            new(householdId, futureDate1, savedDishId, 0),
            new(householdId, futureDate2, savedDishId, 0)
        });

        // Act.
        var result = await _sut.GetStatisticsAsync(
            householdId, savedDishId, today.AddDays(-30), today.AddDays(7));

        // Assert.
        Assert.Equal(0, result.AllTimeTimesCooked);
        Assert.Equal(0, result.TimesCooked);
        Assert.Null(result.LastCookedDate);
        Assert.Null(result.FirstCookedDate);
    }

    private void SetupRepositories(
        Guid householdId,
        Guid savedDishId,
        List<DayPlanDishLink> dishLinks)
    {
        _dayPlanDishLinkRepositoryMock
            .Setup(x => x.GetAllByHouseholdAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dishLinks);

        _savedDishRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SavedDish> { new(savedDishId, householdId, "Pasta", false) });

        _attendanceRepositoryMock
            .Setup(x => x.GetAllByHouseholdAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>());

        _housemateRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Housemate>());
    }
}
