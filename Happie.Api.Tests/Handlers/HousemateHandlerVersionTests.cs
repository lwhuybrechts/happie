using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Results;
using Happie.Api.Domain;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;
using Moq;

namespace Happie.Api.Tests.Handlers;

/// <summary>Unit tests for version-related edge cases in <see cref="HousemateHandler"/>.</summary>
public class HousemateHandlerVersionTests
{
    private readonly Mock<IHousemateRepository> _housemateRepositoryMock = new();
    private readonly Mock<IAttendanceRepository> _attendanceRepositoryMock = new();
    private readonly Mock<ICommentRepository> _commentRepositoryMock = new();
    private readonly HousemateHandler _sut;

    /// <summary>Initializes a new instance of <see cref="HousemateHandlerVersionTests"/> with mocked dependencies.</summary>
    public HousemateHandlerVersionTests()
    {
        _sut = new HousemateHandler(
            _housemateRepositoryMock.Object,
            _attendanceRepositoryMock.Object,
            _commentRepositoryMock.Object);
    }

    /// <summary>When the housemate does not exist, ReportVersionAsync returns NotFound.</summary>
    [Fact]
    public async Task ReportVersionAsync_NonExistentHousemate_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();

        SetupGetHousemate(householdId, housemateId, null);

        // Act.
        var result = await _sut.ReportVersionAsync(householdId, housemateId, "2.0.0");

        // Assert.
        Assert.Equal(ReportVersionOutcome.NotFound, result);
    }

    /// <summary>When the housemate is soft-deleted, ReportVersionAsync returns NotFound.</summary>
    [Fact]
    public async Task ReportVersionAsync_SoftDeletedHousemate_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();

        SetupGetHousemate(householdId, housemateId, CreateHousemate(householdId, housemateId, isDeleted: true));

        // Act.
        var result = await _sut.ReportVersionAsync(householdId, housemateId, "2.0.0");

        // Assert.
        Assert.Equal(ReportVersionOutcome.NotFound, result);
    }

    /// <summary>The handler trusts the IDs passed to it without performing its own auth logic.</summary>
    [Fact]
    public async Task ReportVersionAsync_ValidIds_DoesNotThrow()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();

        SetupGetHousemate(householdId, housemateId, CreateHousemate(householdId, housemateId));

        // Act.
        var result = await _sut.ReportVersionAsync(householdId, housemateId, "2.5.1.12");

        // Assert.
        Assert.Equal(ReportVersionOutcome.Success, result);
    }

    /// <summary>A new Housemate record has null AppVersion by default.</summary>
    [Fact]
    public void Housemate_NewInstance_HasNullAppVersion()
    {
        // Act.
        var housemate = new Housemate(Guid.NewGuid(), Guid.NewGuid(), "Alice", "#E91E63", false);

        // Assert.
        Assert.Null(housemate.AppVersion);
    }

    /// <summary>HousemateDto does not expose an AppVersion property.</summary>
    [Fact]
    public void HousemateDto_DoesNotExposeAppVersionField()
    {
        // Act.
        var appVersionProperty = typeof(HousemateDto).GetProperty("AppVersion");

        // Assert.
        Assert.Null(appVersionProperty);
    }

    private void SetupGetHousemate(Guid householdId, Guid housemateId, Housemate? returns)
    {
        _housemateRepositoryMock
            .Setup(x => x.GetAsync(householdId, housemateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private static Housemate CreateHousemate(Guid householdId, Guid housemateId, bool isDeleted = false) =>
        new(housemateId, householdId, "Alice", HousemateColors.Palette[0], isDeleted);
}
