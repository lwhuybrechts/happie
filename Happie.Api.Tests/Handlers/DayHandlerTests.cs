using ExpectedObjects;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Models;
using Happie.Shared.Domain;
using Moq;

namespace Happie.Api.Tests.Handlers;

/// <summary>Unit tests for <see cref="DayHandler"/>.</summary>
public class DayHandlerTests
{
    private readonly Mock<IHousemateRepository> _housemateRepositoryMock = new();
    private readonly Mock<IAttendanceRepository> _attendanceRepositoryMock = new();
    private readonly Mock<IDishRepository> _dishRepositoryMock = new();
    private readonly Mock<ICommentRepository> _commentRepositoryMock = new();
    private readonly Mock<IDayHistoryRepository> _dayHistoryRepositoryMock = new();
    private readonly DayHandler _sut;

    /// <summary>Initializes a new instance of <see cref="DayHandlerTests"/> with mocked dependencies.</summary>
    public DayHandlerTests()
    {
        _sut = new DayHandler(
            _housemateRepositoryMock.Object,
            _attendanceRepositoryMock.Object,
            _dishRepositoryMock.Object,
            _commentRepositoryMock.Object,
            _dayHistoryRepositoryMock.Object);
    }

    /// <summary>A dish description of exactly 100 characters is accepted and saved.</summary>
    [Fact]
    public async Task UpsertDishAsync_DescriptionExactly100Chars_SavesSuccessfully()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var description = new string('A', 100);

        SetupDishUpsert();
        SetupHistoryAdd();

        // Act.
        await _sut.UpsertDishAsync(householdId, date, description, actingHousemateId);

        // Assert.
        _dishRepositoryMock.Verify(
            x => x.UpsertAsync(It.Is<DishRecord>(r => r.Description == description), actingHousemateId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>A comment text of exactly 200 characters is accepted and saved.</summary>
    [Fact]
    public async Task UpsertCommentAsync_TextExactly200Chars_SavesSuccessfully()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var text = new string('A', 200);

        SetupGetHousemate(householdId, housemateId, CreateHousemate(householdId, housemateId));
        SetupCommentUpsert();
        SetupHistoryAdd();

        // Act.
        var result = await _sut.UpsertCommentAsync(householdId, date, housemateId, text, actingHousemateId);

        // Assert.
        Assert.True(result);
        _commentRepositoryMock.Verify(
            x => x.UpsertAsync(It.Is<Comment>(c => c.Text == text), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>History entries returned by the repository are already in reverse-chronological order.</summary>
    [Fact]
    public async Task GetDayPlanAsync_HistoryEntries_ReturnedInReverseChronologicalOrder()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);

        var t1 = new DateTimeOffset(2025, 7, 15, 10, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2025, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var t3 = new DateTimeOffset(2025, 7, 15, 14, 0, 0, TimeSpan.Zero);

        // Repository returns entries already in reverse-chronological order (most recent first).
        var historyEntries = new List<DayHistoryEntry>
        {
            new(householdId, date, t3, housemateId, ChangeType.Comment, "Comment set."),
            new(householdId, date, t2, housemateId, ChangeType.Dish, "Dish set."),
            new(householdId, date, t1, housemateId, ChangeType.Attendance, "Attendance set."),
        };

        var housemate = CreateHousemate(householdId, housemateId);

        SetupGetAllHousemates(householdId, new List<Housemate> { housemate });
        SetupGetAttendanceByDate(householdId, date, new List<AttendanceRecord>());
        SetupGetDish(householdId, date, null);
        SetupGetCommentsByDate(householdId, date, new List<Comment>());
        SetupGetHistoryByDate(householdId, date, historyEntries);

        // Act.
        var result = await _sut.GetDayPlanAsync(householdId, date);

        // Assert.
        var expectedHistory = new List<HistoryEntryDto>
        {
            new(t3, housemate.Name, ChangeType.Comment, "Comment set."),
            new(t2, housemate.Name, ChangeType.Dish, "Dish set."),
            new(t1, housemate.Name, ChangeType.Attendance, "Attendance set."),
        };

        expectedHistory.ToExpectedObject().ShouldEqual(result.History);
    }

    /// <summary>When the housemate does not exist, UpsertCommentAsync returns false.</summary>
    [Fact]
    public async Task UpsertCommentAsync_HousemateNotFound_ReturnsFalse()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);

        SetupGetHousemate(householdId, housemateId, null);

        // Act.
        var result = await _sut.UpsertCommentAsync(householdId, date, housemateId, "Hello", Guid.NewGuid());

        // Assert.
        Assert.False(result);
    }

    /// <summary>When the housemate does not exist, DeleteCommentAsync returns false.</summary>
    [Fact]
    public async Task DeleteCommentAsync_HousemateNotFound_ReturnsFalse()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);

        SetupGetHousemate(householdId, housemateId, null);

        // Act.
        var result = await _sut.DeleteCommentAsync(householdId, date, housemateId, Guid.NewGuid());

        // Assert.
        Assert.False(result);
    }

    /// <summary>When the housemate exists, DeleteCommentAsync deletes the comment and writes a history entry.</summary>
    [Fact]
    public async Task DeleteCommentAsync_HousemateExists_DeletesCommentAndWritesHistory()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);

        SetupGetHousemate(householdId, housemateId, CreateHousemate(householdId, housemateId));
        SetupCommentDelete();
        SetupHistoryAdd();

        // Act.
        var result = await _sut.DeleteCommentAsync(householdId, date, housemateId, actingHousemateId);

        // Assert.
        Assert.True(result);
        _commentRepositoryMock.Verify(
            x => x.DeleteAsync(householdId, date, housemateId, It.IsAny<CancellationToken>()),
            Times.Once);
        _dayHistoryRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<DayHistoryEntry>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private void SetupGetHousemate(Guid householdId, Guid housemateId, Housemate? returns)
    {
        _housemateRepositoryMock
            .Setup(x => x.GetAsync(householdId, housemateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetAllHousemates(Guid householdId, List<Housemate> returns)
    {
        _housemateRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetAttendanceByDate(Guid householdId, DateOnly date, List<AttendanceRecord> returns)
    {
        _attendanceRepositoryMock
            .Setup(x => x.GetByDateAsync(householdId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetDish(Guid householdId, DateOnly date, DishRecord? returns)
    {
        _dishRepositoryMock
            .Setup(x => x.GetAsync(householdId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetCommentsByDate(Guid householdId, DateOnly date, List<Comment> returns)
    {
        _commentRepositoryMock
            .Setup(x => x.GetByDateAsync(householdId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetHistoryByDate(Guid householdId, DateOnly date, List<DayHistoryEntry> returns)
    {
        _dayHistoryRepositoryMock
            .Setup(x => x.GetByDateAsync(householdId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupDishUpsert()
    {
        _dishRepositoryMock
            .Setup(x => x.UpsertAsync(It.IsAny<DishRecord>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupCommentUpsert()
    {
        _commentRepositoryMock
            .Setup(x => x.UpsertAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupCommentDelete()
    {
        _commentRepositoryMock
            .Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupHistoryAdd()
    {
        _dayHistoryRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<DayHistoryEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static Housemate CreateHousemate(Guid householdId, Guid housemateId) =>
        new(housemateId, householdId, "Alice", HousemateColors.Palette[0], false);
}
