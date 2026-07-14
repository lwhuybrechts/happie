using System.Text;
using Happie.Api.Constants;
using Happie.Api.Functions;
using Happie.Api.Handlers;
using Happie.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;

namespace Happie.Api.Tests.Functions;

/// <summary>Unit tests for <see cref="SavedDishesFunction"/>.</summary>
public class SavedDishesFunctionTests
{
    private readonly Mock<ISavedDishHandler> _savedDishHandlerMock = new();
    private readonly SavedDishesFunction _sut;

    /// <summary>Initializes a new instance of <see cref="SavedDishesFunctionTests"/>.</summary>
    public SavedDishesFunctionTests()
    {
        _sut = new SavedDishesFunction(_savedDishHandlerMock.Object);
    }

    /// <summary>PostAsync with an invalid JSON body returns a BadRequestObjectResult.</summary>
    [Fact]
    public async Task PostAsync_InvalidBody_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext(Guid.NewGuid());
        var request = CreateInvalidJsonRequest();

        // Act.
        var result = await _sut.PostAsync(request, context, CancellationToken.None);

        // Assert.
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(ApiErrorCodes.BadRequest, error.Code);
    }

    /// <summary>PostAsync with a null/empty body returns a BadRequestObjectResult.</summary>
    [Fact]
    public async Task PostAsync_NullBody_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext(Guid.NewGuid());
        var request = CreateNullBodyRequest();

        // Act.
        var result = await _sut.PostAsync(request, context, CancellationToken.None);

        // Assert.
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(ApiErrorCodes.BadRequest, error.Code);
    }

    /// <summary>PutAsync with an invalid GUID returns a BadRequestObjectResult with BAD_REQUEST code.</summary>
    [Fact]
    public async Task PutAsync_InvalidGuid_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext(Guid.NewGuid());
        var request = new DefaultHttpContext().Request;

        // Act.
        var result = await _sut.PutAsync(request, "not-a-guid", context, CancellationToken.None);

        // Assert.
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(ApiErrorCodes.BadRequest, error.Code);
    }

    /// <summary>DeleteAsync with an invalid GUID returns a BadRequestObjectResult with BAD_REQUEST code.</summary>
    [Fact]
    public async Task DeleteAsync_InvalidGuid_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext(Guid.NewGuid());
        var request = new DefaultHttpContext().Request;

        // Act.
        var result = await _sut.DeleteAsync(request, "not-a-guid", context, CancellationToken.None);

        // Assert.
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(ApiErrorCodes.BadRequest, error.Code);
    }

    /// <summary>PutAsync with a null body returns a BadRequestObjectResult.</summary>
    [Fact]
    public async Task PutAsync_NullBody_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext(Guid.NewGuid());
        var request = CreateNullBodyRequest();
        var validGuid = Guid.NewGuid().ToString();

        // Act.
        var result = await _sut.PutAsync(request, validGuid, context, CancellationToken.None);

        // Assert.
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(ApiErrorCodes.BadRequest, error.Code);
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

    private static HttpRequest CreateInvalidJsonRequest()
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("not json"));
        context.Request.ContentLength = 8;
        return context.Request;
    }

    private static HttpRequest CreateNullBodyRequest()
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("null"));
        context.Request.ContentLength = 4;
        return context.Request;
    }
}
