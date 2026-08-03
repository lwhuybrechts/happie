using Happie.Api.Constants;
using Happie.Api.Functions;
using Happie.Api.Handlers;
using Happie.Api.Results;
using Happie.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;

namespace Happie.Api.Tests.Functions;

/// <summary>Unit tests for <see cref="RecipeFunction"/>.</summary>
public class RecipeFunctionTests
{
    private readonly Mock<IRecipeHandler> _recipeHandlerMock = new();
    private readonly RecipeFunction _sut;

    /// <summary>Initializes a new instance of <see cref="RecipeFunctionTests"/>.</summary>
    public RecipeFunctionTests()
    {
        _sut = new RecipeFunction(_recipeHandlerMock.Object);
    }

    /// <summary>GetSummaryAsync with an invalid GUID returns 400 BAD_REQUEST.</summary>
    [Fact]
    public async Task GetSummaryAsync_InvalidGuid_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext(Guid.NewGuid());
        var request = new DefaultHttpContext().Request;

        // Act.
        var result = await _sut.GetSummaryAsync(request, "not-a-guid", context, CancellationToken.None);

        // Assert.
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(ApiErrorCodes.BadRequest, error.Code);
    }

    /// <summary>GetIngredientsAsync with an invalid GUID returns 400 BAD_REQUEST.</summary>
    [Fact]
    public async Task GetIngredientsAsync_InvalidGuid_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext(Guid.NewGuid());
        var request = new DefaultHttpContext().Request;

        // Act.
        var result = await _sut.GetIngredientsAsync(request, "not-a-guid", context, CancellationToken.None);

        // Assert.
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(ApiErrorCodes.BadRequest, error.Code);
    }

    /// <summary>GetInstructionsAsync with an invalid GUID returns 400 BAD_REQUEST.</summary>
    [Fact]
    public async Task GetInstructionsAsync_InvalidGuid_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext(Guid.NewGuid());
        var request = new DefaultHttpContext().Request;

        // Act.
        var result = await _sut.GetInstructionsAsync(request, "not-a-guid", context, CancellationToken.None);

        // Assert.
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(ApiErrorCodes.BadRequest, error.Code);
    }

    /// <summary>GetSummaryAsync returns 404 when handler returns null.</summary>
    [Fact]
    public async Task GetSummaryAsync_DishNotFound_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId);
        var request = new DefaultHttpContext().Request;
        SetupGetSummaryAsync(householdId, savedDishId, null);

        // Act.
        var result = await _sut.GetSummaryAsync(request, savedDishId.ToString(), context, CancellationToken.None);

        // Assert.
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal(ApiErrorCodes.NotFound, error.Code);
    }

    /// <summary>GetIngredientsAsync returns 404 when handler returns null.</summary>
    [Fact]
    public async Task GetIngredientsAsync_DishNotFound_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId);
        var request = new DefaultHttpContext().Request;
        SetupGetIngredientsAsync(householdId, savedDishId, null);

        // Act.
        var result = await _sut.GetIngredientsAsync(request, savedDishId.ToString(), context, CancellationToken.None);

        // Assert.
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal(ApiErrorCodes.NotFound, error.Code);
    }

    /// <summary>GetInstructionsAsync returns 404 when handler returns null.</summary>
    [Fact]
    public async Task GetInstructionsAsync_DishNotFound_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId);
        var request = new DefaultHttpContext().Request;
        SetupGetInstructionsAsync(householdId, savedDishId, null);

        // Act.
        var result = await _sut.GetInstructionsAsync(request, savedDishId.ToString(), context, CancellationToken.None);

        // Assert.
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal(ApiErrorCodes.NotFound, error.Code);
    }

    /// <summary>GetSummaryAsync returns 200 with data when handler returns a response.</summary>
    [Fact]
    public async Task GetSummaryAsync_DishExists_ReturnsOk()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId);
        var request = new DefaultHttpContext().Request;
        var response = new RecipeSummaryResponse("A tasty dish.", 30, 4);
        SetupGetSummaryAsync(householdId, savedDishId, response);

        // Act.
        var result = await _sut.GetSummaryAsync(request, savedDishId.ToString(), context, CancellationToken.None);

        // Assert.
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(response, okResult.Value);
    }

    /// <summary>GetIngredientsAsync returns 200 with data when handler returns a response.</summary>
    [Fact]
    public async Task GetIngredientsAsync_DishExists_ReturnsOk()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId);
        var request = new DefaultHttpContext().Request;
        var response = new IngredientsResponse(new List<IngredientDto>(), new List<IngredientCheckDto>());
        SetupGetIngredientsAsync(householdId, savedDishId, response);

        // Act.
        var result = await _sut.GetIngredientsAsync(request, savedDishId.ToString(), context, CancellationToken.None);

        // Assert.
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(response, okResult.Value);
    }

    /// <summary>GetInstructionsAsync returns 200 with data when handler returns a response.</summary>
    [Fact]
    public async Task GetInstructionsAsync_DishExists_ReturnsOk()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId);
        var request = new DefaultHttpContext().Request;
        var response = new InstructionsResponse(new List<CookingInstructionDto>());
        SetupGetInstructionsAsync(householdId, savedDishId, response);

        // Act.
        var result = await _sut.GetInstructionsAsync(request, savedDishId.ToString(), context, CancellationToken.None);

        // Assert.
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(response, okResult.Value);
    }

    /// <summary>UpdateSummaryAsync with an invalid GUID returns 400 BAD_REQUEST.</summary>
    [Fact]
    public async Task UpdateSummaryAsync_InvalidGuid_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext(Guid.NewGuid());
        var request = new DefaultHttpContext().Request;

        // Act.
        var result = await _sut.UpdateSummaryAsync(request, "not-a-guid", context, CancellationToken.None);

        // Assert.
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(ApiErrorCodes.BadRequest, error.Code);
    }

    /// <summary>UpdateIngredientsAsync with an invalid GUID returns 400 BAD_REQUEST.</summary>
    [Fact]
    public async Task UpdateIngredientsAsync_InvalidGuid_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext(Guid.NewGuid());
        var request = new DefaultHttpContext().Request;

        // Act.
        var result = await _sut.UpdateIngredientsAsync(request, "not-a-guid", context, CancellationToken.None);

        // Assert.
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(ApiErrorCodes.BadRequest, error.Code);
    }

    /// <summary>UpdateIngredientCheckAsync with an invalid dish GUID returns 400 BAD_REQUEST.</summary>
    [Fact]
    public async Task UpdateIngredientCheckAsync_InvalidDishGuid_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext(Guid.NewGuid());
        var request = new DefaultHttpContext().Request;

        // Act.
        var result = await _sut.UpdateIngredientCheckAsync(request, "not-a-guid", Guid.NewGuid().ToString(), context, CancellationToken.None);

        // Assert.
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(ApiErrorCodes.BadRequest, error.Code);
    }

    /// <summary>UpdateIngredientCheckAsync with an invalid ingredient GUID returns 400 BAD_REQUEST.</summary>
    [Fact]
    public async Task UpdateIngredientCheckAsync_InvalidIngredientGuid_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext(Guid.NewGuid());
        var request = new DefaultHttpContext().Request;

        // Act.
        var result = await _sut.UpdateIngredientCheckAsync(request, Guid.NewGuid().ToString(), "not-a-guid", context, CancellationToken.None);

        // Assert.
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(ApiErrorCodes.BadRequest, error.Code);
    }

    /// <summary>UpdateInstructionsAsync with an invalid GUID returns 400 BAD_REQUEST.</summary>
    [Fact]
    public async Task UpdateInstructionsAsync_InvalidGuid_ReturnsBadRequest()
    {
        // Arrange.
        var context = CreateFunctionContext(Guid.NewGuid());
        var request = new DefaultHttpContext().Request;

        // Act.
        var result = await _sut.UpdateInstructionsAsync(request, "not-a-guid", context, CancellationToken.None);

        // Assert.
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(ApiErrorCodes.BadRequest, error.Code);
    }

    /// <summary>UpdateSummaryAsync returns 200 when handler returns Success.</summary>
    [Fact]
    public async Task UpdateSummaryAsync_Success_ReturnsOk()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId);
        var request = HttpRequestFactory.Create(new UpdateSummaryRequest("Summary.", 30, 4));
        SetupUpdateSummaryAsync(householdId, savedDishId, new UpdateSummaryResult(UpdateSummaryOutcome.Success));

        // Act.
        var result = await _sut.UpdateSummaryAsync(request, savedDishId.ToString(), context, CancellationToken.None);

        // Assert.
        Assert.IsType<OkResult>(result);
    }

    /// <summary>UpdateSummaryAsync returns 404 when handler returns NotFound.</summary>
    [Fact]
    public async Task UpdateSummaryAsync_NotFound_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId);
        var request = HttpRequestFactory.Create(new UpdateSummaryRequest("Summary.", 30, 4));
        SetupUpdateSummaryAsync(householdId, savedDishId, new UpdateSummaryResult(UpdateSummaryOutcome.NotFound));

        // Act.
        var result = await _sut.UpdateSummaryAsync(request, savedDishId.ToString(), context, CancellationToken.None);

        // Assert.
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal(ApiErrorCodes.NotFound, error.Code);
    }

    /// <summary>UpdateSummaryAsync returns 422 when handler returns ValidationError.</summary>
    [Fact]
    public async Task UpdateSummaryAsync_ValidationError_ReturnsUnprocessableEntity()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId);
        var request = HttpRequestFactory.Create(new UpdateSummaryRequest("Summary.", 30, 4));
        SetupUpdateSummaryAsync(householdId, savedDishId, new UpdateSummaryResult(UpdateSummaryOutcome.ValidationError));

        // Act.
        var result = await _sut.UpdateSummaryAsync(request, savedDishId.ToString(), context, CancellationToken.None);

        // Assert.
        var unprocessable = Assert.IsType<UnprocessableEntityObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(unprocessable.Value);
        Assert.Equal(ApiErrorCodes.ValidationError, error.Code);
    }

    /// <summary>UpdateIngredientsAsync returns 200 when handler returns Success.</summary>
    [Fact]
    public async Task UpdateIngredientsAsync_Success_ReturnsOk()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId);
        var request = HttpRequestFactory.Create(new UpdateIngredientsRequest(new List<IngredientDto>()));
        SetupUpdateIngredientsAsync(householdId, savedDishId, new UpdateIngredientsResult(UpdateIngredientsOutcome.Success));

        // Act.
        var result = await _sut.UpdateIngredientsAsync(request, savedDishId.ToString(), context, CancellationToken.None);

        // Assert.
        Assert.IsType<OkResult>(result);
    }

    /// <summary>UpdateIngredientsAsync returns 404 when handler returns NotFound.</summary>
    [Fact]
    public async Task UpdateIngredientsAsync_NotFound_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId);
        var request = HttpRequestFactory.Create(new UpdateIngredientsRequest(new List<IngredientDto>()));
        SetupUpdateIngredientsAsync(householdId, savedDishId, new UpdateIngredientsResult(UpdateIngredientsOutcome.NotFound));

        // Act.
        var result = await _sut.UpdateIngredientsAsync(request, savedDishId.ToString(), context, CancellationToken.None);

        // Assert.
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal(ApiErrorCodes.NotFound, error.Code);
    }

    /// <summary>UpdateIngredientsAsync returns 422 when handler returns ValidationError.</summary>
    [Fact]
    public async Task UpdateIngredientsAsync_ValidationError_ReturnsUnprocessableEntity()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId);
        var request = HttpRequestFactory.Create(new UpdateIngredientsRequest(new List<IngredientDto>()));
        SetupUpdateIngredientsAsync(householdId, savedDishId, new UpdateIngredientsResult(UpdateIngredientsOutcome.ValidationError));

        // Act.
        var result = await _sut.UpdateIngredientsAsync(request, savedDishId.ToString(), context, CancellationToken.None);

        // Assert.
        var unprocessable = Assert.IsType<UnprocessableEntityObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(unprocessable.Value);
        Assert.Equal(ApiErrorCodes.ValidationError, error.Code);
    }

    /// <summary>UpdateIngredientCheckAsync returns 200 when handler returns Success.</summary>
    [Fact]
    public async Task UpdateIngredientCheckAsync_Success_ReturnsOk()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId);
        var request = HttpRequestFactory.Create(new UpdateIngredientCheckRequest(true));
        SetupUpdateIngredientCheckAsync(householdId, savedDishId, ingredientId, new UpdateIngredientCheckResult(UpdateIngredientCheckOutcome.Success));

        // Act.
        var result = await _sut.UpdateIngredientCheckAsync(request, savedDishId.ToString(), ingredientId.ToString(), context, CancellationToken.None);

        // Assert.
        Assert.IsType<OkResult>(result);
    }

    /// <summary>UpdateIngredientCheckAsync returns 404 when handler returns NotFound.</summary>
    [Fact]
    public async Task UpdateIngredientCheckAsync_NotFound_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId);
        var request = HttpRequestFactory.Create(new UpdateIngredientCheckRequest(true));
        SetupUpdateIngredientCheckAsync(householdId, savedDishId, ingredientId, new UpdateIngredientCheckResult(UpdateIngredientCheckOutcome.NotFound));

        // Act.
        var result = await _sut.UpdateIngredientCheckAsync(request, savedDishId.ToString(), ingredientId.ToString(), context, CancellationToken.None);

        // Assert.
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal(ApiErrorCodes.NotFound, error.Code);
    }

    /// <summary>UpdateInstructionsAsync returns 200 when handler returns Success.</summary>
    [Fact]
    public async Task UpdateInstructionsAsync_Success_ReturnsOk()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId);
        var request = HttpRequestFactory.Create(new UpdateInstructionsRequest(new List<CookingInstructionDto>()));
        SetupUpdateInstructionsAsync(householdId, savedDishId, new UpdateInstructionsResult(UpdateInstructionsOutcome.Success));

        // Act.
        var result = await _sut.UpdateInstructionsAsync(request, savedDishId.ToString(), context, CancellationToken.None);

        // Assert.
        Assert.IsType<OkResult>(result);
    }

    /// <summary>UpdateInstructionsAsync returns 404 when handler returns NotFound.</summary>
    [Fact]
    public async Task UpdateInstructionsAsync_NotFound_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId);
        var request = HttpRequestFactory.Create(new UpdateInstructionsRequest(new List<CookingInstructionDto>()));
        SetupUpdateInstructionsAsync(householdId, savedDishId, new UpdateInstructionsResult(UpdateInstructionsOutcome.NotFound));

        // Act.
        var result = await _sut.UpdateInstructionsAsync(request, savedDishId.ToString(), context, CancellationToken.None);

        // Assert.
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal(ApiErrorCodes.NotFound, error.Code);
    }

    /// <summary>UpdateInstructionsAsync returns 422 when handler returns ValidationError.</summary>
    [Fact]
    public async Task UpdateInstructionsAsync_ValidationError_ReturnsUnprocessableEntity()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var context = CreateFunctionContext(householdId);
        var request = HttpRequestFactory.Create(new UpdateInstructionsRequest(new List<CookingInstructionDto>()));
        SetupUpdateInstructionsAsync(householdId, savedDishId, new UpdateInstructionsResult(UpdateInstructionsOutcome.ValidationError));

        // Act.
        var result = await _sut.UpdateInstructionsAsync(request, savedDishId.ToString(), context, CancellationToken.None);

        // Assert.
        var unprocessable = Assert.IsType<UnprocessableEntityObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(unprocessable.Value);
        Assert.Equal(ApiErrorCodes.ValidationError, error.Code);
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

    private void SetupGetSummaryAsync(Guid householdId, Guid savedDishId, RecipeSummaryResponse? returns)
    {
        _recipeHandlerMock
            .Setup(x => x.GetSummaryAsync(householdId, savedDishId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetIngredientsAsync(Guid householdId, Guid savedDishId, IngredientsResponse? returns)
    {
        _recipeHandlerMock
            .Setup(x => x.GetIngredientsAsync(householdId, savedDishId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetInstructionsAsync(Guid householdId, Guid savedDishId, InstructionsResponse? returns)
    {
        _recipeHandlerMock
            .Setup(x => x.GetInstructionsAsync(householdId, savedDishId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupUpdateSummaryAsync(Guid householdId, Guid savedDishId, UpdateSummaryResult returns)
    {
        _recipeHandlerMock
            .Setup(x => x.UpdateSummaryAsync(householdId, savedDishId, It.IsAny<UpdateSummaryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupUpdateIngredientsAsync(Guid householdId, Guid savedDishId, UpdateIngredientsResult returns)
    {
        _recipeHandlerMock
            .Setup(x => x.UpdateIngredientsAsync(householdId, savedDishId, It.IsAny<UpdateIngredientsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupUpdateIngredientCheckAsync(Guid householdId, Guid savedDishId, Guid ingredientId, UpdateIngredientCheckResult returns)
    {
        _recipeHandlerMock
            .Setup(x => x.UpdateIngredientCheckAsync(householdId, savedDishId, ingredientId, It.IsAny<UpdateIngredientCheckRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupUpdateInstructionsAsync(Guid householdId, Guid savedDishId, UpdateInstructionsResult returns)
    {
        _recipeHandlerMock
            .Setup(x => x.UpdateInstructionsAsync(householdId, savedDishId, It.IsAny<UpdateInstructionsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }
}
