using Happie.Api.Domain;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Results;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;
using Moq;

namespace Happie.Api.Tests.Handlers;

/// <summary>Unit tests for <see cref="RecipeHandler"/>.</summary>
public class RecipeHandlerTests
{
    private readonly Mock<ISavedDishRepository> _savedDishRepositoryMock = new();
    private readonly Mock<IRecipeSummaryRepository> _recipeSummaryRepositoryMock = new();
    private readonly Mock<IIngredientRepository> _ingredientRepositoryMock = new();
    private readonly Mock<ICookingInstructionRepository> _cookingInstructionRepositoryMock = new();
    private readonly Mock<IIngredientCheckRepository> _ingredientCheckRepositoryMock = new();
    private readonly RecipeHandler _sut;

    public RecipeHandlerTests()
    {
        _sut = new RecipeHandler(
            _savedDishRepositoryMock.Object,
            _recipeSummaryRepositoryMock.Object,
            _ingredientRepositoryMock.Object,
            _cookingInstructionRepositoryMock.Object,
            _ingredientCheckRepositoryMock.Object);
    }

    [Fact]
    public async Task UpdateIngredientsAsync_ExceedsMaxIngredients_ReturnsValidationError()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var ingredients = Enumerable.Range(0, 31)
            .Select(x => new IngredientDto(Guid.NewGuid(), 1.0, UnitOfMeasurement.G, $"Ingredient {x}", x))
            .ToList();
        var request = new UpdateIngredientsRequest(ingredients);

        // Act.
        var result = await _sut.UpdateIngredientsAsync(householdId, savedDishId, request, CancellationToken.None);

        // Assert.
        Assert.Equal(UpdateIngredientsOutcome.ValidationError, result.Outcome);
    }

    [Fact]
    public async Task UpdateInstructionsAsync_ExceedsMaxInstructions_ReturnsValidationError()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var instructions = Enumerable.Range(0, 16)
            .Select(x => new CookingInstructionDto(Guid.NewGuid(), $"Step {x}", x))
            .ToList();
        var request = new UpdateInstructionsRequest(instructions);

        // Act.
        var result = await _sut.UpdateInstructionsAsync(householdId, savedDishId, request, CancellationToken.None);

        // Assert.
        Assert.Equal(UpdateInstructionsOutcome.ValidationError, result.Outcome);
    }

    [Fact]
    public async Task UpdateIngredientsAsync_AmountBelowMinimum_ReturnsValidationError()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var ingredients = new List<IngredientDto>
        {
            new(Guid.NewGuid(), 0.001, UnitOfMeasurement.G, "Salt", 0),
        };
        var request = new UpdateIngredientsRequest(ingredients);

        // Act.
        var result = await _sut.UpdateIngredientsAsync(householdId, savedDishId, request, CancellationToken.None);

        // Assert.
        Assert.Equal(UpdateIngredientsOutcome.ValidationError, result.Outcome);
    }

    [Fact]
    public async Task UpdateIngredientsAsync_AmountAboveMaximum_ReturnsValidationError()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var ingredients = new List<IngredientDto>
        {
            new(Guid.NewGuid(), 10000, UnitOfMeasurement.G, "Flour", 0),
        };
        var request = new UpdateIngredientsRequest(ingredients);

        // Act.
        var result = await _sut.UpdateIngredientsAsync(householdId, savedDishId, request, CancellationToken.None);

        // Assert.
        Assert.Equal(UpdateIngredientsOutcome.ValidationError, result.Outcome);
    }

    [Fact]
    public async Task UpdateIngredientsAsync_NameExceedsMaxLength_ReturnsValidationError()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var longName = new string('A', 101);
        var ingredients = new List<IngredientDto>
        {
            new(Guid.NewGuid(), 1.0, UnitOfMeasurement.G, longName, 0),
        };
        var request = new UpdateIngredientsRequest(ingredients);

        // Act.
        var result = await _sut.UpdateIngredientsAsync(householdId, savedDishId, request, CancellationToken.None);

        // Assert.
        Assert.Equal(UpdateIngredientsOutcome.ValidationError, result.Outcome);
    }

    [Fact]
    public async Task UpdateInstructionsAsync_TextExceedsMaxLength_ReturnsValidationError()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var longText = new string('A', 501);
        var instructions = new List<CookingInstructionDto>
        {
            new(Guid.NewGuid(), longText, 0),
        };
        var request = new UpdateInstructionsRequest(instructions);

        // Act.
        var result = await _sut.UpdateInstructionsAsync(householdId, savedDishId, request, CancellationToken.None);

        // Assert.
        Assert.Equal(UpdateInstructionsOutcome.ValidationError, result.Outcome);
    }

    [Fact]
    public async Task UpdateSummaryAsync_SummaryExceedsMaxLength_ReturnsValidationError()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var longSummary = new string('A', 251);
        var request = new UpdateSummaryRequest(longSummary, null, null);

        // Act.
        var result = await _sut.UpdateSummaryAsync(householdId, savedDishId, request, CancellationToken.None);

        // Assert.
        Assert.Equal(UpdateSummaryOutcome.ValidationError, result.Outcome);
    }

    [Fact]
    public async Task UpdateSummaryAsync_NegativeDuration_ReturnsValidationError()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var request = new UpdateSummaryRequest(null, -1, null);

        // Act.
        var result = await _sut.UpdateSummaryAsync(householdId, savedDishId, request, CancellationToken.None);

        // Assert.
        Assert.Equal(UpdateSummaryOutcome.ValidationError, result.Outcome);
    }

    [Fact]
    public async Task UpdateSummaryAsync_ServingsBelowMinimum_ReturnsValidationError()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var request = new UpdateSummaryRequest(null, null, 0);

        // Act.
        var result = await _sut.UpdateSummaryAsync(householdId, savedDishId, request, CancellationToken.None);

        // Assert.
        Assert.Equal(UpdateSummaryOutcome.ValidationError, result.Outcome);
    }

    [Fact]
    public async Task UpdateSummaryAsync_ServingsAboveMaximum_ReturnsValidationError()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var request = new UpdateSummaryRequest(null, null, 26);

        // Act.
        var result = await _sut.UpdateSummaryAsync(householdId, savedDishId, request, CancellationToken.None);

        // Assert.
        Assert.Equal(UpdateSummaryOutcome.ValidationError, result.Outcome);
    }

    [Fact]
    public async Task UpdateIngredientsAsync_RemovedIngredient_DeletesIngredientCheck()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var removedIngredientId = Guid.NewGuid();
        var keptIngredientId = Guid.NewGuid();

        SetupGetSavedDish(householdId, savedDishId, CreateActiveDish(householdId, savedDishId));
        SetupGetAllIngredients(householdId, savedDishId, new List<Ingredient>
        {
            new(removedIngredientId, householdId, savedDishId, 1.0, UnitOfMeasurement.G, "Removed", 0),
            new(keptIngredientId, householdId, savedDishId, 2.0, UnitOfMeasurement.Ml, "Kept", 1),
        });

        // Only send the kept ingredient in the update request.
        var request = new UpdateIngredientsRequest(new List<IngredientDto>
        {
            new(keptIngredientId, 2.0, UnitOfMeasurement.Ml, "Kept", 0),
        });

        // Act.
        var result = await _sut.UpdateIngredientsAsync(householdId, savedDishId, request, CancellationToken.None);

        // Assert.
        Assert.Equal(UpdateIngredientsOutcome.Success, result.Outcome);
        _ingredientCheckRepositoryMock.Verify(
            x => x.BatchDeleteAsync(
                householdId,
                It.Is<IReadOnlyList<(Guid, Guid)>>(keys =>
                    keys.Count == 1 && keys[0].Item1 == savedDishId && keys[0].Item2 == removedIngredientId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetSummaryAsync_DishSoftDeleted_ReturnsNull()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();

        SetupGetSavedDish(householdId, savedDishId, CreateSoftDeletedDish(householdId, savedDishId));

        // Act.
        var result = await _sut.GetSummaryAsync(householdId, savedDishId, CancellationToken.None);

        // Assert.
        Assert.Null(result);
    }

    [Fact]
    public async Task GetIngredientsAsync_DishSoftDeleted_ReturnsNull()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();

        SetupGetSavedDish(householdId, savedDishId, CreateSoftDeletedDish(householdId, savedDishId));

        // Act.
        var result = await _sut.GetIngredientsAsync(householdId, savedDishId, CancellationToken.None);

        // Assert.
        Assert.Null(result);
    }

    [Fact]
    public async Task GetInstructionsAsync_DishSoftDeleted_ReturnsNull()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();

        SetupGetSavedDish(householdId, savedDishId, CreateSoftDeletedDish(householdId, savedDishId));

        // Act.
        var result = await _sut.GetInstructionsAsync(householdId, savedDishId, CancellationToken.None);

        // Assert.
        Assert.Null(result);
    }

    [Fact]
    public async Task GetSummaryAsync_DishNotFound_ReturnsNull()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();

        SetupGetSavedDish(householdId, savedDishId, null);

        // Act.
        var result = await _sut.GetSummaryAsync(householdId, savedDishId, CancellationToken.None);

        // Assert.
        Assert.Null(result);
    }

    [Fact]
    public async Task GetIngredientsAsync_DishNotFound_ReturnsNull()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();

        SetupGetSavedDish(householdId, savedDishId, null);

        // Act.
        var result = await _sut.GetIngredientsAsync(householdId, savedDishId, CancellationToken.None);

        // Assert.
        Assert.Null(result);
    }

    [Fact]
    public async Task GetInstructionsAsync_DishNotFound_ReturnsNull()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();

        SetupGetSavedDish(householdId, savedDishId, null);

        // Act.
        var result = await _sut.GetInstructionsAsync(householdId, savedDishId, CancellationToken.None);

        // Assert.
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateSummaryAsync_DishNotFound_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var request = new UpdateSummaryRequest("A summary", 30, 4);

        SetupGetSavedDish(householdId, savedDishId, null);

        // Act.
        var result = await _sut.UpdateSummaryAsync(householdId, savedDishId, request, CancellationToken.None);

        // Assert.
        Assert.Equal(UpdateSummaryOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task UpdateIngredientsAsync_DishNotFound_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var request = new UpdateIngredientsRequest(new List<IngredientDto>
        {
            new(Guid.NewGuid(), 1.0, UnitOfMeasurement.G, "Salt", 0),
        });

        SetupGetSavedDish(householdId, savedDishId, null);

        // Act.
        var result = await _sut.UpdateIngredientsAsync(householdId, savedDishId, request, CancellationToken.None);

        // Assert.
        Assert.Equal(UpdateIngredientsOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task UpdateInstructionsAsync_DishNotFound_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var request = new UpdateInstructionsRequest(new List<CookingInstructionDto>
        {
            new(Guid.NewGuid(), "Step 1", 0),
        });

        SetupGetSavedDish(householdId, savedDishId, null);

        // Act.
        var result = await _sut.UpdateInstructionsAsync(householdId, savedDishId, request, CancellationToken.None);

        // Assert.
        Assert.Equal(UpdateInstructionsOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task UpdateIngredientCheckAsync_DishNotFound_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var request = new UpdateIngredientCheckRequest(true);

        SetupGetSavedDish(householdId, savedDishId, null);

        // Act.
        var result = await _sut.UpdateIngredientCheckAsync(householdId, savedDishId, ingredientId, request, CancellationToken.None);

        // Assert.
        Assert.Equal(UpdateIngredientCheckOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task UpdateIngredientsAsync_WhitespaceOnlyNames_FilteredOut()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var validId = Guid.NewGuid();
        var whitespaceId = Guid.NewGuid();

        SetupGetSavedDish(householdId, savedDishId, CreateActiveDish(householdId, savedDishId));
        SetupGetAllIngredients(householdId, savedDishId, new List<Ingredient>());

        var request = new UpdateIngredientsRequest(new List<IngredientDto>
        {
            new(validId, 1.0, UnitOfMeasurement.G, "Flour", 0),
            new(whitespaceId, 2.0, UnitOfMeasurement.Ml, "   ", 1),
        });

        // Act.
        var result = await _sut.UpdateIngredientsAsync(householdId, savedDishId, request, CancellationToken.None);

        // Assert.
        Assert.Equal(UpdateIngredientsOutcome.Success, result.Outcome);
        _ingredientRepositoryMock.Verify(
            x => x.BatchUpsertAsync(
                It.Is<IReadOnlyList<Ingredient>>(list =>
                    list.Count == 1 && list[0].Id == validId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateInstructionsAsync_WhitespaceOnlyText_FilteredOut()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var validId = Guid.NewGuid();
        var whitespaceId = Guid.NewGuid();

        SetupGetSavedDish(householdId, savedDishId, CreateActiveDish(householdId, savedDishId));
        SetupGetAllInstructions(householdId, savedDishId, new List<CookingInstruction>());

        var request = new UpdateInstructionsRequest(new List<CookingInstructionDto>
        {
            new(validId, "Boil water", 0),
            new(whitespaceId, "   \t  ", 1),
        });

        // Act.
        var result = await _sut.UpdateInstructionsAsync(householdId, savedDishId, request, CancellationToken.None);

        // Assert.
        Assert.Equal(UpdateInstructionsOutcome.Success, result.Outcome);
        _cookingInstructionRepositoryMock.Verify(
            x => x.BatchUpsertAsync(
                It.Is<IReadOnlyList<CookingInstruction>>(list =>
                    list.Count == 1 && list[0].Id == validId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private void SetupGetSavedDish(Guid householdId, Guid savedDishId, SavedDish? returns)
    {
        _savedDishRepositoryMock
            .Setup(x => x.GetAsync(householdId, savedDishId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetAllIngredients(Guid householdId, Guid savedDishId, List<Ingredient> returns)
    {
        _ingredientRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, savedDishId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetAllInstructions(Guid householdId, Guid savedDishId, List<CookingInstruction> returns)
    {
        _cookingInstructionRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, savedDishId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private static SavedDish CreateActiveDish(Guid householdId, Guid savedDishId) =>
        new(savedDishId, householdId, "Test Dish", false);

    private static SavedDish CreateSoftDeletedDish(Guid householdId, Guid savedDishId) =>
        new(savedDishId, householdId, "Deleted Dish", true);
}
