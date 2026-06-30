using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
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

// Feature: offline-cache, Property 14: Server conflict detection
/// <summary>
/// For any mutation request containing an If-Unmodified-Since header, if the target entity's
/// LastModified is strictly after the header value, the server should return HTTP 409. If LastModified
/// is at or before the header value (or the entity does not exist), the server should apply the
/// mutation normally and return 2xx.
/// Validates: Requirements 6.11, 6.12
/// </summary>
public class DaysFunctionConflictPropertyTests
{
    private readonly Mock<IDayHandler> _dayHandlerMock = new();
    private readonly Mock<IAttendanceRepository> _attendanceRepositoryMock = new();
    private readonly Mock<IDishRepository> _dishRepositoryMock = new();
    private readonly Mock<ICommentRepository> _commentRepositoryMock = new();
    private readonly DaysFunction _sut;

    /// <summary>Initializes a new instance of <see cref="DaysFunctionConflictPropertyTests"/>.</summary>
    public DaysFunctionConflictPropertyTests()
    {
        _sut = new DaysFunction(
            _dayHandlerMock.Object,
            _attendanceRepositoryMock.Object,
            _dishRepositoryMock.Object,
            _commentRepositoryMock.Object);
    }

    // Feature: offline-cache, Property 14: Server conflict detection
    /// <summary>
    /// When LastModified is strictly after If-Unmodified-Since, the server returns 409 CONFLICT.
    /// Validates: Requirements 6.11, 6.12
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PutAttendanceAsync_LastModifiedAfterHeader_Returns409()
    {
        return Prop.ForAll(
            ConflictTimestampPairArb(),
            async timestampPair =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var housemateId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var context = CreateFunctionContext(householdId, actingHousemateId);
                var request = HttpRequestFactory.Create(new UpdateAttendanceRequest(AttendanceStatus.EatingIn));
                request.Headers["If-Unmodified-Since"] = timestampPair.IfUnmodifiedSince.ToString("R");

                var record = CreateAttendanceRecord(householdId, housemateId, timestampPair.LastModified);
                SetupGetAttendanceAsync(householdId, housemateId, record);

                // Act.
                var result = await _sut.PutAttendanceAsync(request, "2025-07-15", housemateId.ToString(), context, CancellationToken.None);

                // Assert.
                return (result is ObjectResult objectResult
                    && objectResult.StatusCode == 409
                    && objectResult.Value is ApiErrorResponse error
                    && error.Code == ApiErrorCodes.Conflict)
                    .Label($"Expected 409 CONFLICT when LastModified={timestampPair.LastModified:O} > If-Unmodified-Since={timestampPair.IfUnmodifiedSince:O}");
            });
    }

    // Feature: offline-cache, Property 14: Server conflict detection
    /// <summary>
    /// When LastModified is at or before If-Unmodified-Since, the server proceeds normally (not 409).
    /// Validates: Requirements 6.11, 6.12
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PutAttendanceAsync_LastModifiedAtOrBeforeHeader_DoesNotReturn409()
    {
        return Prop.ForAll(
            NoConflictTimestampPairArb(),
            async timestampPair =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var housemateId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var context = CreateFunctionContext(householdId, actingHousemateId);
                var request = HttpRequestFactory.Create(new UpdateAttendanceRequest(AttendanceStatus.EatingIn));
                request.Headers["If-Unmodified-Since"] = timestampPair.IfUnmodifiedSince.ToString("R");

                var record = CreateAttendanceRecord(householdId, housemateId, timestampPair.LastModified);
                SetupGetAttendanceAsync(householdId, housemateId, record);
                SetupUpsertAttendanceAsync(householdId, housemateId);

                // Act.
                var result = await _sut.PutAttendanceAsync(request, "2025-07-15", housemateId.ToString(), context, CancellationToken.None);

                // Assert.
                var is409 = result is ObjectResult obj && obj.StatusCode == 409;
                return (!is409)
                    .Label($"Expected no 409 when LastModified={timestampPair.LastModified:O} <= If-Unmodified-Since={timestampPair.IfUnmodifiedSince:O}");
            });
    }

    // Feature: offline-cache, Property 14: Server conflict detection
    /// <summary>
    /// When the entity does not exist, the server proceeds normally (not 409) regardless of the header value.
    /// Validates: Requirements 6.11, 6.12
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PutAttendanceAsync_EntityDoesNotExist_DoesNotReturn409()
    {
        return Prop.ForAll(
            AnyTimestampArb(),
            async ifUnmodifiedSince =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var housemateId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var context = CreateFunctionContext(householdId, actingHousemateId);
                var request = HttpRequestFactory.Create(new UpdateAttendanceRequest(AttendanceStatus.EatingIn));
                request.Headers["If-Unmodified-Since"] = ifUnmodifiedSince.ToString("R");

                SetupGetAttendanceAsync(householdId, housemateId, null);
                SetupUpsertAttendanceAsync(householdId, housemateId);

                // Act.
                var result = await _sut.PutAttendanceAsync(request, "2025-07-15", housemateId.ToString(), context, CancellationToken.None);

                // Assert.
                var is409 = result is ObjectResult obj && obj.StatusCode == 409;
                return (!is409)
                    .Label($"Expected no 409 when entity does not exist (If-Unmodified-Since={ifUnmodifiedSince:O})");
            });
    }

    /// <summary>Generates pairs where LastModified is strictly after If-Unmodified-Since (conflict case).</summary>
    private static Arbitrary<TimestampPair> ConflictTimestampPairArb()
    {
        var gen = Gen.Choose(1, 1_000_000)
            .SelectMany(baseTicks =>
                Gen.Choose(1, 1_000_000)
                    .Select(offsetTicks =>
                    {
                        var ifUnmodifiedSince = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(baseTicks);
                        var lastModified = ifUnmodifiedSince.AddSeconds(offsetTicks);
                        return new TimestampPair(lastModified, ifUnmodifiedSince);
                    }));

        return Arb.From(gen);
    }

    /// <summary>Generates pairs where LastModified is at or before If-Unmodified-Since (no conflict case).</summary>
    private static Arbitrary<TimestampPair> NoConflictTimestampPairArb()
    {
        var gen = Gen.Choose(1, 1_000_000)
            .SelectMany(baseTicks =>
                Gen.Choose(0, 1_000_000)
                    .Select(offsetTicks =>
                    {
                        var lastModified = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(baseTicks);
                        var ifUnmodifiedSince = lastModified.AddSeconds(offsetTicks);
                        return new TimestampPair(lastModified, ifUnmodifiedSince);
                    }));

        return Arb.From(gen);
    }

    /// <summary>Generates any valid DateTimeOffset for testing the entity-not-found case.</summary>
    private static Arbitrary<DateTimeOffset> AnyTimestampArb()
    {
        var gen = Gen.Choose(1, 2_000_000)
            .Select(ticks => new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(ticks));

        return Arb.From(gen);
    }

    private void SetupGetAttendanceAsync(Guid householdId, Guid housemateId, AttendanceRecord? returns)
    {
        _attendanceRepositoryMock
            .Setup(x => x.GetAsync(householdId, It.IsAny<DateOnly>(), housemateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupUpsertAttendanceAsync(Guid householdId, Guid housemateId)
    {
        _dayHandlerMock
            .Setup(x => x.UpsertAttendanceAsync(householdId, It.IsAny<DateOnly>(), housemateId, It.IsAny<AttendanceStatus>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
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

    private static AttendanceRecord CreateAttendanceRecord(Guid householdId, Guid housemateId, DateTimeOffset lastModified)
    {
        return new AttendanceRecord(householdId, housemateId, new DateOnly(2025, 7, 15), AttendanceStatus.EatingIn, false, lastModified);
    }

    /// <summary>Holds a pair of timestamps for property test generation.</summary>
    private record TimestampPair(DateTimeOffset LastModified, DateTimeOffset IfUnmodifiedSince);
}
