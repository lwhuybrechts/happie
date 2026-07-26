using Happie.Api.Constants;
using Happie.Api.Domain;
using Happie.Api.Functions;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Results;
using Happie.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;

namespace Happie.Api.Tests.Functions;

/// <summary>Unit tests for <see cref="StatisticsFunction"/>.</summary>
public class StatisticsFunctionTests
{
    private readonly Mock<IDishStatisticsHandler> _dishStatisticsHandlerMock = new();
    private readonly Mock<IHousemateStatisticsHandler> _housemateStatisticsHandlerMock = new();
    private readonly Mock<ISavedDishRepository> _savedDishRepositoryMock = new();
    private readonly Mock<IHousemateRepository> _housemateRepositoryMock = new();
    private readonly StatisticsFunction _sut;

    /// <summary>Initializes a new instance of <see cref="StatisticsFunctionTests"/>.</summary>
    public StatisticsFunctionTests()
    {
        _sut = new StatisticsFunction(
            _dishStatisticsHandlerMock.Object,
            _housemateStatisticsHandlerMock.Object,
            _savedDishRepositoryMock.Object,
            _housemateRepositoryMock.Object);
    }

    /// <summary>GetDishStatisticsAsync with an invalid GUID returns NotFound.</summary>
    [Fact]
    public async Task GetDishStatisticsAsync_InvalidGuid_ReturnsNotFound()
    {
        // Arrange.
        var context = CreateFunctionContext();
        var request = CreateRequestWithQuery("2024-01-01", "2024-01-31");

        // Act.
        var result = await _sut.GetDishStatisticsAsync(request, "not-a-guid", context, CancellationToken.None);

        // Assert.
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal(ApiErrorCodes.NotFound, error.Code);
    }

    /// <summary>GetDishStatisticsAsync with missing date parameters returns BadRequest.</summary>
    [Fact]
    public async Task GetDishStatisticsAsync_MissingDates_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext();
        var request = CreateRequestWithQuery(null, null);

        // Act.
        var result = await _sut.GetDishStatisticsAsync(request, Guid.NewGuid().ToString(), context, CancellationToken.None);

        // Assert.
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(ApiErrorCodes.BadRequest, error.Code);
    }

    /// <summary>GetDishStatisticsAsync with invalid date format returns BadRequest.</summary>
    [Fact]
    public async Task GetDishStatisticsAsync_InvalidDateFormat_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext();
        var request = CreateRequestWithQuery("not-a-date", "2024-01-31");

        // Act.
        var result = await _sut.GetDishStatisticsAsync(request, Guid.NewGuid().ToString(), context, CancellationToken.None);

        // Assert.
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(ApiErrorCodes.BadRequest, error.Code);
    }

    /// <summary>GetDishStatisticsAsync with from after to returns BadRequest.</summary>
    [Fact]
    public async Task GetDishStatisticsAsync_FromAfterTo_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext();
        var request = CreateRequestWithQuery("2024-02-01", "2024-01-01");

        // Act.
        var result = await _sut.GetDishStatisticsAsync(request, Guid.NewGuid().ToString(), context, CancellationToken.None);

        // Assert.
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(ApiErrorCodes.BadRequest, error.Code);
    }

    /// <summary>GetDishStatisticsAsync with non-existent dish returns NotFound.</summary>
    [Fact]
    public async Task GetDishStatisticsAsync_NonExistentDish_ReturnsNotFound()
    {
        // Arrange.
        var context = CreateFunctionContext();
        var savedDishId = Guid.NewGuid();
        var request = CreateRequestWithQuery("2024-01-01", "2024-01-31");
        SetupSavedDishGetAsync(savedDishId, null);

        // Act.
        var result = await _sut.GetDishStatisticsAsync(request, savedDishId.ToString(), context, CancellationToken.None);

        // Assert.
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal(ApiErrorCodes.NotFound, error.Code);
    }

    /// <summary>GetDishStatisticsAsync with soft-deleted dish returns NotFound.</summary>
    [Fact]
    public async Task GetDishStatisticsAsync_DeletedDish_ReturnsNotFound()
    {
        // Arrange.
        var context = CreateFunctionContext();
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];
        var savedDishId = Guid.NewGuid();
        var request = CreateRequestWithQuery("2024-01-01", "2024-01-31");
        SetupSavedDishGetAsync(savedDishId, new SavedDish(savedDishId, householdId, "Pasta", true));

        // Act.
        var result = await _sut.GetDishStatisticsAsync(request, savedDishId.ToString(), context, CancellationToken.None);

        // Assert.
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal(ApiErrorCodes.NotFound, error.Code);
    }

    /// <summary>GetDishStatisticsAsync with valid parameters delegates to handler and maps response.</summary>
    [Fact]
    public async Task GetDishStatisticsAsync_ValidRequest_ReturnsMappedResponse()
    {
        // Arrange.
        var context = CreateFunctionContext();
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];
        var savedDishId = Guid.NewGuid();
        var request = CreateRequestWithQuery("2024-01-01", "2024-01-31");
        SetupSavedDishGetAsync(savedDishId, new SavedDish(savedDishId, householdId, "Pasta", false));

        var handlerResult = new DishStatisticsResult(5, 12, new DateOnly(2024, 1, 28), new DateOnly(2024, 1, 5), new List<CookingShareEntry>());
        SetupDishStatisticsHandler(householdId, savedDishId, handlerResult);

        // Act.
        var result = await _sut.GetDishStatisticsAsync(request, savedDishId.ToString(), context, CancellationToken.None);

        // Assert.
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<DishStatisticsResponse>(okResult.Value);
        Assert.Equal(5, response.TimesCooked);
        Assert.Equal(12, response.AllTimeTimesCooked);
        Assert.Equal("2024-01-28", response.LastCookedDate);
    }

    /// <summary>GetHousemateStatisticsAsync with an invalid GUID returns NotFound.</summary>
    [Fact]
    public async Task GetHousemateStatisticsAsync_InvalidGuid_ReturnsNotFound()
    {
        // Arrange.
        var context = CreateFunctionContext();
        var request = CreateRequestWithQuery("2024-01-01", "2024-01-31");

        // Act.
        var result = await _sut.GetHousemateStatisticsAsync(request, "not-a-guid", context, CancellationToken.None);

        // Assert.
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal(ApiErrorCodes.NotFound, error.Code);
    }

    /// <summary>GetHousemateStatisticsAsync with missing date parameters returns BadRequest.</summary>
    [Fact]
    public async Task GetHousemateStatisticsAsync_MissingDates_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext();
        var request = CreateRequestWithQuery(null, null);

        // Act.
        var result = await _sut.GetHousemateStatisticsAsync(request, Guid.NewGuid().ToString(), context, CancellationToken.None);

        // Assert.
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(ApiErrorCodes.BadRequest, error.Code);
    }

    /// <summary>GetHousemateStatisticsAsync with invalid date format returns BadRequest.</summary>
    [Fact]
    public async Task GetHousemateStatisticsAsync_InvalidDateFormat_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext();
        var request = CreateRequestWithQuery("2024-01-01", "bad-date");

        // Act.
        var result = await _sut.GetHousemateStatisticsAsync(request, Guid.NewGuid().ToString(), context, CancellationToken.None);

        // Assert.
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(ApiErrorCodes.BadRequest, error.Code);
    }

    /// <summary>GetHousemateStatisticsAsync with from after to returns BadRequest.</summary>
    [Fact]
    public async Task GetHousemateStatisticsAsync_FromAfterTo_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext();
        var request = CreateRequestWithQuery("2024-03-01", "2024-01-01");

        // Act.
        var result = await _sut.GetHousemateStatisticsAsync(request, Guid.NewGuid().ToString(), context, CancellationToken.None);

        // Assert.
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(ApiErrorCodes.BadRequest, error.Code);
    }

    /// <summary>GetHousemateStatisticsAsync with non-existent housemate returns NotFound.</summary>
    [Fact]
    public async Task GetHousemateStatisticsAsync_NonExistentHousemate_ReturnsNotFound()
    {
        // Arrange.
        var context = CreateFunctionContext();
        var housemateId = Guid.NewGuid();
        var request = CreateRequestWithQuery("2024-01-01", "2024-01-31");
        SetupHousemateGetAsync(housemateId, null);

        // Act.
        var result = await _sut.GetHousemateStatisticsAsync(request, housemateId.ToString(), context, CancellationToken.None);

        // Assert.
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal(ApiErrorCodes.NotFound, error.Code);
    }

    /// <summary>GetHousemateStatisticsAsync with valid parameters delegates to handler and maps response.</summary>
    [Fact]
    public async Task GetHousemateStatisticsAsync_ValidRequest_ReturnsMappedResponse()
    {
        // Arrange.
        var context = CreateFunctionContext();
        var householdId = (Guid)context.Items[FunctionContextKeys.HouseholdId];
        var housemateId = Guid.NewGuid();
        var otherHousemateId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var request = CreateRequestWithQuery("2024-01-01", "2024-01-31");
        SetupHousemateGetAsync(housemateId, new Housemate(housemateId, householdId, "Alice", "#E91E63", false));

        var handlerResult = new HousemateStatisticsResult(
            8, 20, 25, 6, 20, 3, 4,
            new List<CookingShareEntry>
            {
                new(housemateId, "Alice", "#E91E63", 8),
                new(otherHousemateId, "Bob", "#2196F3", 5),
            },
            new List<TopDishEntry>
            {
                new(savedDishId, "Pasta", 4),
            });
        SetupHousemateStatisticsHandler(householdId, housemateId, handlerResult);

        // Act.
        var result = await _sut.GetHousemateStatisticsAsync(request, housemateId.ToString(), context, CancellationToken.None);

        // Assert.
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<HousemateStatisticsResponse>(okResult.Value);
        Assert.Equal(8, response.TimesCooked);
        Assert.Equal(20, response.AllTimeTimesCooked);
        Assert.Equal(25, response.DaysEatingIn);
        Assert.Equal(6, response.CookRatioDays);
        Assert.Equal(20, response.CookRatioEatingInDays);
        Assert.Equal(3, response.LongestStreak);
        Assert.Equal(4, response.BusiestWeek);
        Assert.Equal(2, response.CookingShares.Count);
        Assert.Equal(housemateId, response.CookingShares[0].HousemateId);
        Assert.Equal(8, response.CookingShares[0].ChefDayCount);
        Assert.Equal(otherHousemateId, response.CookingShares[1].HousemateId);
        Assert.Single(response.TopDishes);
        Assert.Equal(savedDishId, response.TopDishes[0].SavedDishId);
        Assert.Equal("Pasta", response.TopDishes[0].Description);
        Assert.Equal(4, response.TopDishes[0].Count);
    }

    private void SetupSavedDishGetAsync(Guid savedDishId, SavedDish? returns)
    {
        _savedDishRepositoryMock
            .Setup(x => x.GetAsync(It.IsAny<Guid>(), savedDishId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupHousemateGetAsync(Guid housemateId, Housemate? returns)
    {
        _housemateRepositoryMock
            .Setup(x => x.GetAsync(It.IsAny<Guid>(), housemateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupDishStatisticsHandler(Guid householdId, Guid savedDishId, DishStatisticsResult returns)
    {
        _dishStatisticsHandlerMock
            .Setup(x => x.GetStatisticsAsync(
                householdId,
                savedDishId,
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupHousemateStatisticsHandler(Guid householdId, Guid housemateId, HousemateStatisticsResult returns)
    {
        _housemateStatisticsHandlerMock
            .Setup(x => x.GetStatisticsAsync(
                householdId,
                housemateId,
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private static FunctionContext CreateFunctionContext()
    {
        var householdId = Guid.NewGuid();
        var items = new Dictionary<object, object>
        {
            [FunctionContextKeys.HouseholdId] = householdId,
        };
        var contextMock = new Mock<FunctionContext>();
        contextMock.Setup(x => x.Items).Returns(items);
        return contextMock.Object;
    }

    private static HttpRequest CreateRequestWithQuery(string? from, string? to)
    {
        var httpContext = new DefaultHttpContext();
        var queryParameters = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>();

        if (from is not null)
            queryParameters["from"] = from;
        if (to is not null)
            queryParameters["to"] = to;

        httpContext.Request.Query = new QueryCollection(queryParameters);
        return httpContext.Request;
    }
}
