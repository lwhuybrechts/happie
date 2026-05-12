using Happie.Api.Constants;
using Happie.Api.Functions;
using Happie.Api.Handlers;
using Happie.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;

namespace Happie.Api.Tests.Functions;

/// <summary>Unit tests for <see cref="HousematesFunction"/>.</summary>
public class HousematesFunctionTests
{
    private readonly Mock<IHousemateHandler> _housemateHandlerMock = new();
    private readonly HousematesFunction _sut;

    /// <summary>Initializes a new instance of <see cref="HousematesFunctionTests"/> with a mocked housemate handler.</summary>
    public HousematesFunctionTests()
    {
        _sut = new HousematesFunction(_housemateHandlerMock.Object);
    }

    /// <summary>When the handler returns ColorConflict, the function returns HTTP 409.</summary>
    [Fact]
    public async Task PatchAsync_ColorConflict_ReturnsConflict()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();

        SetupUpdateHousemateAsync(householdId, housemateId, new UpdateHousemateResult(UpdateHousemateOutcome.ColorConflict, ErrorMessage: "This color is already in use by another housemate."));

        var request = HttpRequestFactory.Create(new UpdateHousemateRequest(null, "#FF0000"));
        var context = CreateFunctionContext(householdId);

        // Act.
        var result = await _sut.PatchAsync(request, housemateId.ToString(), context, CancellationToken.None);

        // Assert.
        Assert.IsType<ConflictObjectResult>(result);
    }

    /// <summary>When the handler returns NotFound, the function returns HTTP 404.</summary>
    [Fact]
    public async Task PatchAsync_HousemateNotFound_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();

        SetupUpdateHousemateAsync(householdId, housemateId, new UpdateHousemateResult(UpdateHousemateOutcome.NotFound));

        var request = HttpRequestFactory.Create(new UpdateHousemateRequest("New Name", null));
        var context = CreateFunctionContext(householdId);

        // Act.
        var result = await _sut.PatchAsync(request, housemateId.ToString(), context, CancellationToken.None);

        // Assert.
        Assert.IsType<NotFoundObjectResult>(result);
    }

    /// <summary>When the handler returns NotFound, the delete function returns HTTP 404.</summary>
    [Fact]
    public async Task DeleteAsync_HousemateNotFound_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();

        SetupDeleteHousemateAsync(householdId, housemateId, DeleteHousemateOutcome.NotFound);

        var request = HttpRequestFactory.Create<object?>(null);
        var context = CreateFunctionContext(householdId);

        // Act.
        var result = await _sut.DeleteAsync(request, housemateId.ToString(), context, CancellationToken.None);

        // Assert.
        Assert.IsType<NotFoundObjectResult>(result);
    }

    /// <summary>When the handler returns Success, the delete function returns HTTP 204.</summary>
    [Fact]
    public async Task DeleteAsync_Success_ReturnsNoContent()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();

        SetupDeleteHousemateAsync(householdId, housemateId, DeleteHousemateOutcome.Success);

        var request = HttpRequestFactory.Create<object?>(null);
        var context = CreateFunctionContext(householdId);

        // Act.
        var result = await _sut.DeleteAsync(request, housemateId.ToString(), context, CancellationToken.None);

        // Assert.
        Assert.IsType<NoContentResult>(result);
    }

    private void SetupUpdateHousemateAsync(Guid householdId, Guid housemateId, UpdateHousemateResult returns)
    {
        _housemateHandlerMock
            .Setup(x => x.UpdateHousemateAsync(householdId, housemateId, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupDeleteHousemateAsync(Guid householdId, Guid housemateId, DeleteHousemateOutcome returns)
    {
        _housemateHandlerMock
            .Setup(x => x.DeleteHousemateAsync(householdId, housemateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private static FunctionContext CreateFunctionContext(Guid householdId)
    {
        var items = new Dictionary<object, object>
        {
            [FunctionContextKeys.HouseholdId] = householdId,
        };

        var contextMock = new Mock<FunctionContext>();
        contextMock.Setup(x => x.Items).Returns(items);

        return contextMock.Object;
    }
}
