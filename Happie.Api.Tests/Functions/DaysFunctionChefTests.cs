using Happie.Api.Constants;
using Happie.Api.Functions;
using Happie.Api.Handlers;
using Happie.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;

namespace Happie.Api.Tests.Functions;

/// <summary>Unit tests for <see cref="DaysFunction.PutChefStatusAsync"/>.</summary>
public class DaysFunctionChefTests
{
    private readonly Mock<IDayHandler> _dayHandlerMock = new();
    private readonly DaysFunction _sut;

    /// <summary>Initializes a new instance of <see cref="DaysFunctionChefTests"/> with a mocked day handler.</summary>
    public DaysFunctionChefTests()
    {
        _sut = new DaysFunction(_dayHandlerMock.Object);
    }

    /// <summary>An invalid date route parameter returns HTTP 400.</summary>
    [Fact]
    public async Task PutChefStatusAsync_InvalidDate_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext(Guid.NewGuid(), Guid.NewGuid());
        var request = HttpRequestFactory.Create(new UpdateChefStatusRequest(true));

        // Act.
        var result = await _sut.PutChefStatusAsync(request, "not-a-date", Guid.NewGuid().ToString(), context, CancellationToken.None);

        // Assert.
        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>An invalid GUID route parameter returns HTTP 404.</summary>
    [Fact]
    public async Task PutChefStatusAsync_InvalidGuid_ReturnsNotFound()
    {
        // Arrange.
        var context = CreateFunctionContext(Guid.NewGuid(), Guid.NewGuid());
        var request = HttpRequestFactory.Create(new UpdateChefStatusRequest(true));

        // Act.
        var result = await _sut.PutChefStatusAsync(request, "2025-07-15", "not-a-guid", context, CancellationToken.None);

        // Assert.
        Assert.IsType<NotFoundObjectResult>(result);
    }

    /// <summary>A null request body returns HTTP 400.</summary>
    [Fact]
    public async Task PutChefStatusAsync_NullBody_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext(Guid.NewGuid(), Guid.NewGuid());
        var request = HttpRequestFactory.Create<object?>(null);

        // Act.
        var result = await _sut.PutChefStatusAsync(request, "2025-07-15", Guid.NewGuid().ToString(), context, CancellationToken.None);

        // Assert.
        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>A valid request where the handler returns true yields HTTP 204.</summary>
    [Fact]
    public async Task PutChefStatusAsync_ValidRequest_ReturnsNoContent()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var targetHousemateId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId, actingHousemateId);
        var request = HttpRequestFactory.Create(new UpdateChefStatusRequest(true));

        SetupUpsertChefStatusAsync(householdId, new DateOnly(2025, 7, 15), targetHousemateId, true, actingHousemateId, true);

        // Act.
        var result = await _sut.PutChefStatusAsync(request, "2025-07-15", targetHousemateId.ToString(), context, CancellationToken.None);

        // Assert.
        Assert.IsType<NoContentResult>(result);
    }

    /// <summary>When the handler returns false (housemate not found), the function returns HTTP 404.</summary>
    [Fact]
    public async Task PutChefStatusAsync_HousemateNotFound_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var targetHousemateId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId, actingHousemateId);
        var request = HttpRequestFactory.Create(new UpdateChefStatusRequest(false));

        SetupUpsertChefStatusAsync(householdId, new DateOnly(2025, 7, 15), targetHousemateId, false, actingHousemateId, false);

        // Act.
        var result = await _sut.PutChefStatusAsync(request, "2025-07-15", targetHousemateId.ToString(), context, CancellationToken.None);

        // Assert.
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal(ApiErrorCodes.NotFound, error.Code);
    }

    private void SetupUpsertChefStatusAsync(Guid householdId, DateOnly date, Guid housemateId, bool isChef, Guid actingHousemateId, bool returns)
    {
        _dayHandlerMock
            .Setup(x => x.UpsertChefStatusAsync(householdId, date, housemateId, isChef, actingHousemateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
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
