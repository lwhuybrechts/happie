using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Constants;
using Happie.Api.Functions;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;

namespace Happie.Api.Tests.Functions;

/// <summary>Unit tests and property-based tests for <see cref="DaysFunction"/>.</summary>
public class DaysFunctionTests
{
    private readonly Mock<IDayHandler> _dayHandlerMock = new();
    private readonly Mock<IAttendanceRepository> _attendanceRepositoryMock = new();
    private readonly Mock<IDishRepository> _dishRepositoryMock = new();
    private readonly Mock<ICommentRepository> _commentRepositoryMock = new();
    private readonly DaysFunction _sut;

    /// <summary>Initializes a new instance of <see cref="DaysFunctionTests"/> with a mocked day handler.</summary>
    public DaysFunctionTests()
    {
        _sut = new DaysFunction(
            _dayHandlerMock.Object,
            _attendanceRepositoryMock.Object,
            _dishRepositoryMock.Object,
            _commentRepositoryMock.Object);
    }

    // Feature: happie, Property 10: Dish length validation
    /// <summary>
    /// For any dish description whose trimmed length exceeds 100 characters, the endpoint must return
    /// HTTP 422 with VALIDATION_ERROR. For any description of 100 characters or fewer, it must be accepted.
    /// Validates: Requirements 5.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PutDishAsync_TooLongDescription_ReturnsUnprocessableEntity()
    {
        return Prop.ForAll(
            TooLongDishDescriptionArb(),
            async description =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var context = CreateFunctionContext(householdId, actingHousemateId);
                var request = HttpRequestFactory.Create(new UpdateDishRequest(description, null, null, 0, null));

                // Act.
                var result = await _sut.PutDishAsync(request, "2025-07-15", context, CancellationToken.None);

                // Assert.
                return (result is UnprocessableEntityObjectResult unprocessable
                    && unprocessable.Value is ApiErrorResponse error
                    && error.Code == ApiErrorCodes.ValidationError)
                    .Label($"Expected 422 VALIDATION_ERROR for description of length {description.Trim().Length}");
            });
    }

    // Feature: happie, Property 10: Dish length validation
    /// <summary>
    /// For any dish description whose trimmed length is at most 100 characters, the endpoint must not
    /// return HTTP 422.
    /// Validates: Requirements 5.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PutDishAsync_ValidDescription_DoesNotReturnUnprocessableEntity()
    {
        return Prop.ForAll(
            ValidDishDescriptionArb(),
            async description =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var context = CreateFunctionContext(householdId, actingHousemateId);
                var request = HttpRequestFactory.Create(new UpdateDishRequest(description, null, null, 0, null));

                _dayHandlerMock
                    .Setup(x => x.UpsertDishAsync(householdId, It.IsAny<DateOnly>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<TimeOnly?>(), It.IsAny<int>(), actingHousemateId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Happie.Api.Results.DishUpsertResult.Success);

                // Act.
                var result = await _sut.PutDishAsync(request, "2025-07-15", context, CancellationToken.None);

                // Assert.
                return (result is not UnprocessableEntityObjectResult)
                    .Label($"Expected no 422 for description of trimmed length {description.Trim().Length}");
            });
    }

    /// <summary>DeleteDishAsync with an invalid date format returns a bad request.</summary>
    [Fact]
    public async Task DeleteDishAsync_InvalidDate_ReturnsBadRequest()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId, actingHousemateId);
        var request = new DefaultHttpContext().Request;

        // Act.
        var result = await _sut.DeleteDishAsync(request, "not-a-date", context, CancellationToken.None);

        // Assert.
        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>DeleteDishAsync with a valid date delegates to the handler and returns no content.</summary>
    [Fact]
    public async Task DeleteDishAsync_ValidDate_ReturnsNoContent()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId, actingHousemateId);
        var request = new DefaultHttpContext().Request;

        _dayHandlerMock
            .Setup(x => x.DeleteDishAsync(householdId, It.IsAny<DateOnly>(), actingHousemateId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act.
        var result = await _sut.DeleteDishAsync(request, "2025-07-15", context, CancellationToken.None);

        // Assert.
        Assert.IsType<NoContentResult>(result);
        _dayHandlerMock.Verify(
            x => x.DeleteDishAsync(householdId, new DateOnly(2025, 7, 15), actingHousemateId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Feature: happie, Property 13: Comment length validation
    /// <summary>
    /// For any comment text whose trimmed length exceeds 200 characters, the endpoint must return
    /// HTTP 422 with VALIDATION_ERROR.
    /// Validates: Requirements 6.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PutCommentAsync_TooLongText_ReturnsUnprocessableEntity()
    {
        return Prop.ForAll(
            TooLongCommentTextArb(),
            async text =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var housemateId = Guid.NewGuid();
                var context = CreateFunctionContext(householdId, actingHousemateId);
                var request = HttpRequestFactory.Create(new UpdateCommentRequest(text));

                // Act.
                var result = await _sut.PutCommentAsync(request, "2025-07-15", housemateId.ToString(), context, CancellationToken.None);

                // Assert.
                return (result is UnprocessableEntityObjectResult unprocessable
                    && unprocessable.Value is ApiErrorResponse error
                    && error.Code == ApiErrorCodes.ValidationError)
                    .Label($"Expected 422 VALIDATION_ERROR for comment of trimmed length {text.Trim().Length}");
            });
    }

    // Feature: happie, Property 13: Comment length validation
    /// <summary>
    /// For any comment text whose trimmed length is at most 200 characters, the endpoint must not
    /// return HTTP 422.
    /// Validates: Requirements 6.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PutCommentAsync_ValidText_DoesNotReturnUnprocessableEntity()
    {
        return Prop.ForAll(
            ValidCommentTextArb(),
            async text =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var housemateId = Guid.NewGuid();
                var context = CreateFunctionContext(householdId, actingHousemateId);
                var request = HttpRequestFactory.Create(new UpdateCommentRequest(text));

                _dayHandlerMock
                    .Setup(x => x.UpsertCommentAsync(householdId, It.IsAny<DateOnly>(), housemateId, It.IsAny<string>(), actingHousemateId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                // Act.
                var result = await _sut.PutCommentAsync(request, "2025-07-15", housemateId.ToString(), context, CancellationToken.None);

                // Assert.
                return (result is not UnprocessableEntityObjectResult)
                    .Label($"Expected no 422 for comment of trimmed length {text.Trim().Length}");
            });
    }

    /// <summary>Generates dish descriptions whose trimmed length exceeds 100 characters (101–200).</summary>
    private static Arbitrary<string> TooLongDishDescriptionArb()
    {
        var gen = Gen.Choose(101, 200)
            .SelectMany(len =>
                Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                             'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                             'u', 'v', 'w', 'x', 'y', 'z')
                    .ArrayOf(len)
                    .Select(chars => new string(chars)));

        return Arb.From(gen);
    }

    /// <summary>Generates dish descriptions whose trimmed length is at most 100 characters (1–100).</summary>
    private static Arbitrary<string> ValidDishDescriptionArb()
    {
        var gen = Gen.Choose(1, 100)
            .SelectMany(len =>
                Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                             'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                             'u', 'v', 'w', 'x', 'y', 'z')
                    .ArrayOf(len)
                    .Select(chars => new string(chars)));

        return Arb.From(gen);
    }

    /// <summary>Generates comment texts whose trimmed length exceeds 200 characters (201–300).</summary>
    private static Arbitrary<string> TooLongCommentTextArb()
    {
        var gen = Gen.Choose(201, 300)
            .SelectMany(len =>
                Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                             'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                             'u', 'v', 'w', 'x', 'y', 'z')
                    .ArrayOf(len)
                    .Select(chars => new string(chars)));

        return Arb.From(gen);
    }

    /// <summary>Generates comment texts whose trimmed length is at most 200 characters (1–200).</summary>
    private static Arbitrary<string> ValidCommentTextArb()
    {
        var gen = Gen.Choose(1, 200)
            .SelectMany(len =>
                Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                             'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                             'u', 'v', 'w', 'x', 'y', 'z')
                    .ArrayOf(len)
                    .Select(chars => new string(chars)));

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
