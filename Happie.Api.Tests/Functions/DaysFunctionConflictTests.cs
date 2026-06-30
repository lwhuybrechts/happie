using Happie.Api.Constants;
using Happie.Api.Domain;
using Happie.Api.Functions;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;

namespace Happie.Api.Tests.Functions;

/// <summary>Unit tests for conflict detection in <see cref="DaysFunction"/>.</summary>
public class DaysFunctionConflictTests
{
    private readonly Mock<IDayHandler> _dayHandlerMock = new();
    private readonly Mock<IAttendanceRepository> _attendanceRepositoryMock = new();
    private readonly Mock<IDishRepository> _dishRepositoryMock = new();
    private readonly Mock<ICommentRepository> _commentRepositoryMock = new();
    private readonly DaysFunction _sut;

    /// <summary>Initializes a new instance of <see cref="DaysFunctionConflictTests"/>.</summary>
    public DaysFunctionConflictTests()
    {
        _sut = new DaysFunction(
            _dayHandlerMock.Object,
            _attendanceRepositoryMock.Object,
            _dishRepositoryMock.Object,
            _commentRepositoryMock.Object);
    }

    [Fact]
    public async Task PutAttendanceAsync_EntityDoesNotExist_ProceedsNormally()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var ifUnmodifiedSince = DateTimeOffset.UtcNow;
        var context = CreateFunctionContext(householdId, actingHousemateId);
        var request = HttpRequestFactory.Create(new UpdateAttendanceRequest(AttendanceStatus.EatingIn));
        request.Headers["If-Unmodified-Since"] = ifUnmodifiedSince.ToString("R");

        SetupGetAttendance(householdId, housemateId, null);
        SetupUpsertAttendance(true);

        // Act.
        var result = await _sut.PutAttendanceAsync(request, "2025-07-15", housemateId.ToString(), context, CancellationToken.None);

        // Assert.
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task PutAttendanceAsync_TimestampsExactlyEqual_ProceedsNormally()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        // Use a timestamp truncated to seconds so it round-trips through the RFC 1123 header format.
        var timestamp = new DateTimeOffset(2025, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var context = CreateFunctionContext(householdId, actingHousemateId);
        var request = HttpRequestFactory.Create(new UpdateAttendanceRequest(AttendanceStatus.EatingIn));
        request.Headers["If-Unmodified-Since"] = timestamp.ToString("R");

        var record = CreateAttendanceRecord(householdId, housemateId, timestamp);
        SetupGetAttendance(householdId, housemateId, record);
        SetupUpsertAttendance(true);

        // Act.
        var result = await _sut.PutAttendanceAsync(request, "2025-07-15", housemateId.ToString(), context, CancellationToken.None);

        // Assert.
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task PutAttendanceAsync_EntityModifiedAfterHeaderValue_Returns409()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var ifUnmodifiedSince = DateTimeOffset.UtcNow;
        var entityLastModified = ifUnmodifiedSince.AddSeconds(1);
        var context = CreateFunctionContext(householdId, actingHousemateId);
        var request = HttpRequestFactory.Create(new UpdateAttendanceRequest(AttendanceStatus.EatingIn));
        request.Headers["If-Unmodified-Since"] = ifUnmodifiedSince.ToString("R");

        var record = CreateAttendanceRecord(householdId, housemateId, entityLastModified);
        SetupGetAttendance(householdId, housemateId, record);

        // Act.
        var result = await _sut.PutAttendanceAsync(request, "2025-07-15", housemateId.ToString(), context, CancellationToken.None);

        // Assert.
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
        var errorResponse = Assert.IsType<ApiErrorResponse>(objectResult.Value);
        Assert.Equal(ApiErrorCodes.Conflict, errorResponse.Code);
    }

    private void SetupGetAttendance(Guid householdId, Guid housemateId, AttendanceRecord? returns)
    {
        _attendanceRepositoryMock
            .Setup(x => x.GetAsync(householdId, It.IsAny<DateOnly>(), housemateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupUpsertAttendance(bool returns)
    {
        _dayHandlerMock
            .Setup(x => x.UpsertAttendanceAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<Guid>(), It.IsAny<AttendanceStatus>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private static AttendanceRecord CreateAttendanceRecord(Guid householdId, Guid housemateId, DateTimeOffset lastModified)
    {
        return new AttendanceRecord(householdId, housemateId, new DateOnly(2025, 7, 15), AttendanceStatus.EatingIn, false, lastModified);
    }

    private static FunctionContext CreateFunctionContext(Guid householdId, Guid housemateId)
    {
        var items = new Dictionary<object, object>
        {
            [FunctionContextKeys.HouseholdId] = householdId,
            [FunctionContextKeys.HousemateId] = housemateId,
        };

        var contextMock = new Mock<FunctionContext>();
        contextMock.Setup(x => x.Items).Returns(items);

        return contextMock.Object;
    }
}
