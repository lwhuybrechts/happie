using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Constants;
using Happie.Api.Functions;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;

namespace Happie.Api.Tests.Functions;

// Feature: dinner-time, Property 1: Dinner time validation correctness
/// <summary>
/// Property-based tests for dinner time validation in <see cref="DaysFunction.PutDishAsync"/>.
/// For any pair of nullable integers (hour, minute), the validation function SHALL accept the pair
/// if and only if: (a) both are null, OR (b) both are provided with hour ∈ [0, 23] and minute ∈ [0, 59].
/// All other combinations (one null, out-of-range values) SHALL be rejected.
/// Validates: Requirements 2.7, 5.4, 5.5, 5.9
/// </summary>
public class DaysFunctionDinnerTimeValidationPropertyTests
{
    private readonly Mock<IDayHandler> _dayHandlerMock = new();
    private readonly Mock<IAttendanceRepository> _attendanceRepositoryMock = new();
    private readonly Mock<IDishRepository> _dishRepositoryMock = new();
    private readonly Mock<ICommentRepository> _commentRepositoryMock = new();
    private readonly DaysFunction _sut;

    public DaysFunctionDinnerTimeValidationPropertyTests()
    {
        _sut = new DaysFunction(
            _dayHandlerMock.Object,
            _attendanceRepositoryMock.Object,
            _dishRepositoryMock.Object,
            _commentRepositoryMock.Object);
    }

    /// <summary>
    /// When both dinnerTimeHour and dinnerTimeMinute are null, the validation SHALL accept the pair.
    /// Validates: Requirements 2.7, 5.4, 5.5, 5.9
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PutDishAsync_BothNull_IsAccepted()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(0)),
            async _ =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var context = CreateFunctionContext(householdId, actingHousemateId);
                var request = HttpRequestFactory.Create(new UpdateDishRequest("Valid dish", null, null, 0, null));

                _dayHandlerMock
                    .Setup(x => x.UpsertDishAsync(householdId, It.IsAny<DateOnly>(), It.IsAny<string?>(), It.IsAny<TimeOnly?>(), It.IsAny<int>(), actingHousemateId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Happie.Api.Results.DishUpsertResult.Success);

                // Act.
                var result = await _sut.PutDishAsync(request, "2025-07-15", context, CancellationToken.None);

                // Assert.
                return (result is not UnprocessableEntityObjectResult)
                    .Label("Expected both-null dinner time pair to be accepted");
            });
    }

    /// <summary>
    /// When both dinnerTimeHour and dinnerTimeMinute are provided with valid ranges
    /// (hour ∈ [0, 23], minute ∈ [0, 59]), the validation SHALL accept the pair.
    /// Validates: Requirements 2.7, 5.4, 5.5, 5.9
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PutDishAsync_BothProvidedWithValidRange_IsAccepted()
    {
        return Prop.ForAll(
            ValidDinnerTimePairArb(),
            async pair =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var context = CreateFunctionContext(householdId, actingHousemateId);
                var request = HttpRequestFactory.Create(new UpdateDishRequest("Valid dish", pair.Hour, pair.Minute, 0, null));

                _dayHandlerMock
                    .Setup(x => x.UpsertDishAsync(householdId, It.IsAny<DateOnly>(), It.IsAny<string?>(), It.IsAny<TimeOnly?>(), It.IsAny<int>(), actingHousemateId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Happie.Api.Results.DishUpsertResult.Success);

                // Act.
                var result = await _sut.PutDishAsync(request, "2025-07-15", context, CancellationToken.None);

                // Assert.
                return (result is not UnprocessableEntityObjectResult)
                    .Label($"Expected valid pair (hour={pair.Hour}, minute={pair.Minute}) to be accepted");
            });
    }

    /// <summary>
    /// When only one of dinnerTimeHour or dinnerTimeMinute is provided while the other is null,
    /// the validation SHALL reject the pair with HTTP 422 VALIDATION_ERROR.
    /// Validates: Requirements 2.7, 5.4, 5.5, 5.9
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PutDishAsync_OneNullOneProvided_IsRejected()
    {
        return Prop.ForAll(
            MismatchedNullPairArb(),
            async pair =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var context = CreateFunctionContext(householdId, actingHousemateId);
                var request = HttpRequestFactory.Create(new UpdateDishRequest("Valid dish", pair.Hour, pair.Minute, 0, null));

                // Act.
                var result = await _sut.PutDishAsync(request, "2025-07-15", context, CancellationToken.None);

                // Assert.
                return (result is UnprocessableEntityObjectResult unprocessable
                    && unprocessable.Value is ApiErrorResponse error
                    && error.Code == ApiErrorCodes.ValidationError)
                    .Label($"Expected mismatched pair (hour={pair.Hour}, minute={pair.Minute}) to be rejected with 422");
            });
    }

    /// <summary>
    /// When both dinnerTimeHour and dinnerTimeMinute are provided but at least one is out of range
    /// (hour outside [0, 23] or minute outside [0, 59]), the validation SHALL reject the pair
    /// with HTTP 422 VALIDATION_ERROR.
    /// Validates: Requirements 2.7, 5.4, 5.5, 5.9
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PutDishAsync_OutOfRangeValues_IsRejected()
    {
        return Prop.ForAll(
            OutOfRangeDinnerTimePairArb(),
            async pair =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var context = CreateFunctionContext(householdId, actingHousemateId);
                var request = HttpRequestFactory.Create(new UpdateDishRequest("Valid dish", pair.Hour, pair.Minute, 0, null));

                // Act.
                var result = await _sut.PutDishAsync(request, "2025-07-15", context, CancellationToken.None);

                // Assert.
                return (result is UnprocessableEntityObjectResult unprocessable
                    && unprocessable.Value is ApiErrorResponse error
                    && error.Code == ApiErrorCodes.ValidationError)
                    .Label($"Expected out-of-range pair (hour={pair.Hour}, minute={pair.Minute}) to be rejected with 422");
            });
    }

    /// <summary>Generates valid dinner time pairs with hour ∈ [0, 23] and minute ∈ [0, 59].</summary>
    private static Arbitrary<(int? Hour, int? Minute)> ValidDinnerTimePairArb()
    {
        var gen = Gen.Choose(0, 23)
            .SelectMany(hour => Gen.Choose(0, 59)
                .Select(minute => ((int?)hour, (int?)minute)));

        return Arb.From(gen);
    }

    /// <summary>Generates mismatched null pairs where exactly one of hour/minute is null.</summary>
    private static Arbitrary<(int? Hour, int? Minute)> MismatchedNullPairArb()
    {
        var hourOnlyGen = Gen.Choose(0, 23)
            .Select(hour => ((int?)hour, (int?)null));

        var minuteOnlyGen = Gen.Choose(0, 59)
            .Select(minute => ((int?)null, (int?)minute));

        var gen = Gen.OneOf(hourOnlyGen, minuteOnlyGen);
        return Arb.From(gen);
    }

    /// <summary>
    /// Generates pairs where both are provided but at least one is out of valid range.
    /// Covers: hour &lt; 0, hour &gt; 23, minute &lt; 0, minute &gt; 59.
    /// </summary>
    private static Arbitrary<(int? Hour, int? Minute)> OutOfRangeDinnerTimePairArb()
    {
        // Hour out of range (negative).
        var hourNegativeGen = Gen.Choose(-100, -1)
            .SelectMany(hour => Gen.Choose(0, 59)
                .Select(minute => ((int?)hour, (int?)minute)));

        // Hour out of range (too high).
        var hourTooHighGen = Gen.Choose(24, 100)
            .SelectMany(hour => Gen.Choose(0, 59)
                .Select(minute => ((int?)hour, (int?)minute)));

        // Minute out of range (negative).
        var minuteNegativeGen = Gen.Choose(0, 23)
            .SelectMany(hour => Gen.Choose(-100, -1)
                .Select(minute => ((int?)hour, (int?)minute)));

        // Minute out of range (too high).
        var minuteTooHighGen = Gen.Choose(0, 23)
            .SelectMany(hour => Gen.Choose(60, 100)
                .Select(minute => ((int?)hour, (int?)minute)));

        var gen = Gen.OneOf(hourNegativeGen, hourTooHighGen, minuteNegativeGen, minuteTooHighGen);
        return Arb.From(gen);
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
