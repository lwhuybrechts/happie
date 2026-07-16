using Happie.Api.Domain;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Happie.Api.Tests.Handlers;

/// <summary>Unit tests for <see cref="SavedDishHandler"/>.</summary>
public class SavedDishHandlerTests
{
    private readonly Mock<ISavedDishRepository> _savedDishRepositoryMock = new();
    private readonly Mock<IDishRepository> _dishRepositoryMock = new();
    private readonly SavedDishHandler _sut;

    public SavedDishHandlerTests()
    {
        _sut = new SavedDishHandler(
            _savedDishRepositoryMock.Object,
            _dishRepositoryMock.Object,
            NullLogger<SavedDishHandler>.Instance);
    }

    [Fact]
    public async Task CreateAsync_EmptyDescription_ReturnsValidationError()
    {
        // Arrange.
        var householdId = Guid.NewGuid();

        // Act.
        var result = await _sut.CreateAsync(householdId, "");

        // Assert.
        Assert.Equal(SavedDishCreateOutcome.ValidationError, result.Outcome);
    }

    [Fact]
    public async Task CreateAsync_WhitespaceOnlyDescription_ReturnsValidationError()
    {
        // Arrange.
        var householdId = Guid.NewGuid();

        // Act.
        var result = await _sut.CreateAsync(householdId, "   ");

        // Assert.
        Assert.Equal(SavedDishCreateOutcome.ValidationError, result.Outcome);
    }

    [Fact]
    public async Task CreateAsync_DescriptionExceeds100Chars_ReturnsValidationError()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var description = new string('A', 101);

        // Act.
        var result = await _sut.CreateAsync(householdId, description);

        // Assert.
        Assert.Equal(SavedDishCreateOutcome.ValidationError, result.Outcome);
    }

    [Fact]
    public async Task CreateAsync_MatchesActiveDish_ReturnsAlreadyExists()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var existingDish = CreateSavedDish(householdId, "Pasta Carbonara");

        SetupGetAllSavedDishes(householdId, new List<SavedDish> { existingDish });

        // Act.
        var result = await _sut.CreateAsync(householdId, "pasta carbonara");

        // Assert.
        Assert.Equal(SavedDishCreateOutcome.AlreadyExists, result.Outcome);
    }

    [Fact]
    public async Task CreateAsync_MatchesSoftDeletedDish_ReactivatesRecord()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var deletedDishId = Guid.NewGuid();
        var deletedDish = new SavedDish(deletedDishId, householdId, "pasta carbonara", true);

        SetupGetAllSavedDishes(householdId, new List<SavedDish> { deletedDish });
        SetupGetAllDishRecords(householdId, new List<DishRecord>());

        // Act.
        var result = await _sut.CreateAsync(householdId, "Pasta Carbonara");

        // Assert.
        Assert.Equal(SavedDishCreateOutcome.Reactivated, result.Outcome);
        Assert.NotNull(result.SavedDish);
        Assert.Equal(deletedDishId, result.SavedDish.Id);
        Assert.False(result.SavedDish.IsDeleted);
        Assert.Equal("Pasta Carbonara", result.SavedDish.Description);
    }

    [Fact]
    public async Task CreateAsync_NewDescription_CreatesNewDish()
    {
        // Arrange.
        var householdId = Guid.NewGuid();

        SetupGetAllSavedDishes(householdId, new List<SavedDish>());
        SetupGetAllDishRecords(householdId, new List<DishRecord>());

        // Act.
        var result = await _sut.CreateAsync(householdId, "Spaghetti Bolognese");

        // Assert.
        Assert.Equal(SavedDishCreateOutcome.Created, result.Outcome);
        Assert.NotNull(result.SavedDish);
        Assert.Equal("Spaghetti Bolognese", result.SavedDish.Description);
        Assert.False(result.SavedDish.IsDeleted);
    }

    [Fact]
    public async Task CreateAsync_TrimsDescription_PreservesCallerCasing()
    {
        // Arrange.
        var householdId = Guid.NewGuid();

        SetupGetAllSavedDishes(householdId, new List<SavedDish>());
        SetupGetAllDishRecords(householdId, new List<DishRecord>());

        // Act.
        var result = await _sut.CreateAsync(householdId, "  Pasta Carbonara  ");

        // Assert.
        Assert.Equal(SavedDishCreateOutcome.Created, result.Outcome);
        Assert.NotNull(result.SavedDish);
        Assert.Equal("Pasta Carbonara", result.SavedDish.Description);
    }

    [Fact]
    public async Task UpdateAsync_SameDescriptionDifferentCasing_Succeeds()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var existingDish = new SavedDish(savedDishId, householdId, "pasta", false);

        SetupGetSavedDish(householdId, savedDishId, existingDish);
        SetupGetAllSavedDishes(householdId, new List<SavedDish> { existingDish });

        // Act.
        var result = await _sut.UpdateAsync(householdId, savedDishId, "Pasta");

        // Assert.
        Assert.Equal(SavedDishUpdateOutcome.Updated, result.Outcome);
        Assert.NotNull(result.SavedDish);
        Assert.Equal("Pasta", result.SavedDish.Description);
    }

    [Fact]
    public async Task UpdateAsync_MatchesOtherDish_ReturnsAlreadyExists()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var otherDishId = Guid.NewGuid();
        var targetDish = new SavedDish(savedDishId, householdId, "Pasta", false);
        var otherDish = new SavedDish(otherDishId, householdId, "Risotto", false);

        SetupGetSavedDish(householdId, savedDishId, targetDish);
        SetupGetAllSavedDishes(householdId, new List<SavedDish> { targetDish, otherDish });

        // Act.
        var result = await _sut.UpdateAsync(householdId, savedDishId, "risotto");

        // Assert.
        Assert.Equal(SavedDishUpdateOutcome.AlreadyExists, result.Outcome);
    }

    [Fact]
    public async Task UpdateAsync_DishNotFound_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();

        SetupGetSavedDish(householdId, savedDishId, null);

        // Act.
        var result = await _sut.UpdateAsync(householdId, savedDishId, "Pasta");

        // Assert.
        Assert.Equal(SavedDishUpdateOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task UpdateAsync_DishSoftDeleted_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var deletedDish = new SavedDish(savedDishId, householdId, "Pasta", true);

        SetupGetSavedDish(householdId, savedDishId, deletedDish);

        // Act.
        var result = await _sut.UpdateAsync(householdId, savedDishId, "Risotto");

        // Assert.
        Assert.Equal(SavedDishUpdateOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task UpdateAsync_EmptyDescription_ReturnsValidationError()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();

        // Act.
        var result = await _sut.UpdateAsync(householdId, savedDishId, "");

        // Assert.
        Assert.Equal(SavedDishUpdateOutcome.ValidationError, result.Outcome);
    }

    [Fact]
    public async Task UpdateAsync_ValidDescription_ReturnsUpdated()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var existingDish = new SavedDish(savedDishId, householdId, "Pasta", false);

        SetupGetSavedDish(householdId, savedDishId, existingDish);
        SetupGetAllSavedDishes(householdId, new List<SavedDish> { existingDish });

        // Act.
        var result = await _sut.UpdateAsync(householdId, savedDishId, "  Risotto  ");

        // Assert.
        Assert.Equal(SavedDishUpdateOutcome.Updated, result.Outcome);
        Assert.NotNull(result.SavedDish);
        Assert.Equal("Risotto", result.SavedDish.Description);
    }

    [Fact]
    public async Task DeleteAsync_ActiveDish_SetsIsDeletedTrue()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var activeDish = new SavedDish(savedDishId, householdId, "Pasta", false);

        SetupGetSavedDish(householdId, savedDishId, activeDish);

        // Act.
        var result = await _sut.DeleteAsync(householdId, savedDishId);

        // Assert.
        Assert.Equal(SavedDishDeleteResult.Deleted, result);
        _savedDishRepositoryMock.Verify(
            x => x.UpsertAsync(
                It.Is<SavedDish>(d => d.Id == savedDishId && d.IsDeleted),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_DishNotFound_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();

        SetupGetSavedDish(householdId, savedDishId, null);

        // Act.
        var result = await _sut.DeleteAsync(householdId, savedDishId);

        // Assert.
        Assert.Equal(SavedDishDeleteResult.NotFound, result);
    }

    [Fact]
    public async Task DeleteAsync_AlreadySoftDeleted_ReturnsNotFound()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDishId = Guid.NewGuid();
        var deletedDish = new SavedDish(savedDishId, householdId, "Pasta", true);

        SetupGetSavedDish(householdId, savedDishId, deletedDish);

        // Act.
        var result = await _sut.DeleteAsync(householdId, savedDishId);

        // Assert.
        Assert.Equal(SavedDishDeleteResult.NotFound, result);
    }

    [Fact]
    public async Task GetSuggestionsAsync_ExcludesSavedDishMatches()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var savedDish = CreateSavedDish(householdId, "Pasta");
        var dishRecords = new List<DishRecord>
        {
            CreateDishRecord(householdId, DateOnly.FromDateTime(DateTime.Today), "Pasta"),
            CreateDishRecord(householdId, DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), "Risotto"),
        };

        SetupGetAllDishRecords(householdId, dishRecords);
        SetupGetAllSavedDishes(householdId, new List<SavedDish> { savedDish });

        // Act.
        var result = await _sut.GetSuggestionsAsync(householdId);

        // Assert.
        Assert.Single(result);
        Assert.Equal("Risotto", result[0]);
    }

    [Fact]
    public async Task GetSuggestionsAsync_LimitsToFive()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var dishRecords = Enumerable.Range(0, 10)
            .Select(x => CreateDishRecord(householdId, DateOnly.FromDateTime(DateTime.Today.AddDays(-x)), $"Dish {x}"))
            .ToList();

        SetupGetAllDishRecords(householdId, dishRecords);
        SetupGetAllSavedDishes(householdId, new List<SavedDish>());

        // Act.
        var result = await _sut.GetSuggestionsAsync(householdId);

        // Assert.
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task GetSuggestionsAsync_OrdersByMostRecent()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var dishRecords = new List<DishRecord>
        {
            CreateDishRecord(householdId, DateOnly.FromDateTime(DateTime.Today.AddDays(-3)), "Oldest"),
            CreateDishRecord(householdId, DateOnly.FromDateTime(DateTime.Today), "Newest"),
            CreateDishRecord(householdId, DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), "Middle"),
        };

        SetupGetAllDishRecords(householdId, dishRecords);
        SetupGetAllSavedDishes(householdId, new List<SavedDish>());

        // Act.
        var result = await _sut.GetSuggestionsAsync(householdId);

        // Assert.
        Assert.Equal("Newest", result[0]);
        Assert.Equal("Middle", result[1]);
        Assert.Equal("Oldest", result[2]);
    }

    private void SetupGetAllSavedDishes(Guid householdId, List<SavedDish> returns)
    {
        _savedDishRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetSavedDish(Guid householdId, Guid savedDishId, SavedDish? returns)
    {
        _savedDishRepositoryMock
            .Setup(x => x.GetAsync(householdId, savedDishId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetAllDishRecords(Guid householdId, List<DishRecord> returns)
    {
        _dishRepositoryMock
            .Setup(x => x.GetAllByPartitionAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private static SavedDish CreateSavedDish(Guid householdId, string description) =>
        new(Guid.NewGuid(), householdId, description, false);

    private static DishRecord CreateDishRecord(Guid householdId, DateOnly date, string description) =>
        new(householdId, date, description, null, null, null, null);
}
