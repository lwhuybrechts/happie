using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Results;
using Happie.Api.Domain;
using Happie.Shared.Domain;
using Moq;

namespace Happie.Api.Tests.Handlers;

/// <summary>Unit tests for <see cref="HousemateHandler"/>.</summary>
public class HousemateHandlerTests
{
    private readonly Mock<IHousemateRepository> _housemateRepositoryMock = new();
    private readonly Mock<IAttendanceRepository> _attendanceRepositoryMock = new();
    private readonly Mock<ICommentRepository> _commentRepositoryMock = new();
    private readonly HousemateHandler _sut;

    /// <summary>Initializes a new instance of <see cref="HousemateHandlerTests"/> with mocked dependencies.</summary>
    public HousemateHandlerTests()
    {
        _sut = new HousemateHandler(
            _housemateRepositoryMock.Object,
            _attendanceRepositoryMock.Object,
            _commentRepositoryMock.Object);
    }

    /// <summary>When no attendance records and no comments exist, the housemate is hard-deleted.</summary>
    [Fact]
    public async Task DeleteHousemateAsync_NoLinkedRecords_HardDeletesHousemate()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();

        SetupGetHousemate(householdId, housemateId, CreateHousemate(householdId, housemateId));
        SetupGetAllAttendanceByHousehold(householdId, new List<AttendanceRecord>());
        SetupGetAllCommentsByHousehold(householdId, new List<Comment>());

        // Act.
        await _sut.DeleteHousemateAsync(householdId, housemateId);

        // Assert.
        _housemateRepositoryMock.Verify(
            x => x.DeleteAsync(householdId, housemateId, It.IsAny<CancellationToken>()),
            Times.Once);
        _housemateRepositoryMock.Verify(
            x => x.UpsertAsync(It.IsAny<Housemate>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>When attendance records exist for the housemate, a soft delete is performed.</summary>
    [Fact]
    public async Task DeleteHousemateAsync_HasAttendanceRecords_SoftDeletesHousemate()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();

        SetupGetHousemate(householdId, housemateId, CreateHousemate(householdId, housemateId));
        SetupGetAllAttendanceByHousehold(householdId, new List<AttendanceRecord>
        {
            new(householdId, housemateId, DateOnly.FromDateTime(DateTime.Today), AttendanceStatus.EatingIn),
        });

        // Act.
        await _sut.DeleteHousemateAsync(householdId, housemateId);

        // Assert.
        _housemateRepositoryMock.Verify(
            x => x.UpsertAsync(
                It.Is<Housemate>(h => h.IsDeleted),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _housemateRepositoryMock.Verify(
            x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>When comments exist for the housemate, a soft delete is performed.</summary>
    [Fact]
    public async Task DeleteHousemateAsync_HasComments_SoftDeletesHousemate()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();

        SetupGetHousemate(householdId, housemateId, CreateHousemate(householdId, housemateId));
        SetupGetAllAttendanceByHousehold(householdId, new List<AttendanceRecord>());
        SetupGetAllCommentsByHousehold(householdId, new List<Comment>
        {
            new(householdId, housemateId, DateOnly.FromDateTime(DateTime.Today), "Great dinner!"),
        });

        // Act.
        await _sut.DeleteHousemateAsync(householdId, housemateId);

        // Assert.
        _housemateRepositoryMock.Verify(
            x => x.UpsertAsync(
                It.Is<Housemate>(h => h.IsDeleted),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _housemateRepositoryMock.Verify(
            x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>When the housemate does not exist, NotFound is returned.</summary>
    [Fact]
    public async Task DeleteHousemateAsync_HousemateNotFound_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();

        SetupGetHousemate(householdId, housemateId, null);

        // Act.
        var result = await _sut.DeleteHousemateAsync(householdId, housemateId);

        // Assert.
        Assert.Equal(DeleteHousemateOutcome.NotFound, result);
    }

    /// <summary>When the housemate is already soft-deleted, NotFound is returned.</summary>
    [Fact]
    public async Task DeleteHousemateAsync_AlreadyDeleted_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();

        SetupGetHousemate(householdId, housemateId, CreateHousemate(householdId, housemateId, isDeleted: true));

        // Act.
        var result = await _sut.DeleteHousemateAsync(householdId, housemateId);

        // Assert.
        Assert.Equal(DeleteHousemateOutcome.NotFound, result);
    }

    /// <summary>An empty name returns null.</summary>
    [Fact]
    public async Task AddHousemateAsync_EmptyName_ReturnsNull()
    {
        // Arrange.
        var householdId = Guid.NewGuid();

        // Act.
        var result = await _sut.AddHousemateAsync(householdId, "");

        // Assert.
        Assert.Null(result);
    }

    /// <summary>A whitespace-only name returns null.</summary>
    [Fact]
    public async Task AddHousemateAsync_WhitespaceOnlyName_ReturnsNull()
    {
        // Arrange.
        var householdId = Guid.NewGuid();

        // Act.
        var result = await _sut.AddHousemateAsync(householdId, "   ");

        // Assert.
        Assert.Null(result);
    }

    /// <summary>A name of exactly 50 characters succeeds and returns a housemate.</summary>
    [Fact]
    public async Task AddHousemateAsync_NameExactly50Chars_ReturnsHousemate()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var name = new string('A', 50);

        SetupGetAllHousemates(householdId, new List<Housemate>());

        // Act.
        var result = await _sut.AddHousemateAsync(householdId, name);

        // Assert.
        Assert.NotNull(result);
    }

    /// <summary>A name of exactly 51 characters returns null.</summary>
    [Fact]
    public async Task AddHousemateAsync_NameExactly51Chars_ReturnsNull()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var name = new string('A', 51);

        // Act.
        var result = await _sut.AddHousemateAsync(householdId, name);

        // Assert.
        Assert.Null(result);
    }

    /// <summary>When the requested color is already in use, ColorConflict is returned.</summary>
    [Fact]
    public async Task UpdateHousemateAsync_ColorAlreadyInUse_ReturnsColorConflict()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var otherHousemateId = Guid.NewGuid();
        var takenColor = HousemateColors.Palette[1];

        SetupGetHousemate(householdId, housemateId, CreateHousemate(householdId, housemateId, color: HousemateColors.Palette[0]));
        SetupGetAllHousemates(householdId, new List<Housemate>
        {
            CreateHousemate(householdId, housemateId, color: HousemateColors.Palette[0]),
            CreateHousemate(householdId, otherHousemateId, color: takenColor),
        });

        // Act.
        var result = await _sut.UpdateHousemateAsync(householdId, housemateId, null, takenColor);

        // Assert.
        Assert.Equal(UpdateHousemateOutcome.ColorConflict, result.Outcome);
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

    private void SetupGetAllAttendanceByHousehold(Guid householdId, List<AttendanceRecord> returns)
    {
        _attendanceRepositoryMock
            .Setup(x => x.GetAllByHouseholdAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetAllCommentsByHousehold(Guid householdId, List<Comment> returns)
    {
        _commentRepositoryMock
            .Setup(x => x.GetAllByHouseholdAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private static Housemate CreateHousemate(Guid householdId, Guid housemateId, string? color = null, bool isDeleted = false) =>
        new(housemateId, householdId, "Alice", color ?? HousemateColors.Palette[0], isDeleted);
}
