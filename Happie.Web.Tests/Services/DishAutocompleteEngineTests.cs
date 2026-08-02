using Happie.Shared.Contracts;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

public class DishAutocompleteEngineTests
{
    [Fact]
    public void GetSuggestion_EmptyActiveSegment_ReturnsNull()
    {
        // Arrange.
        var dishes = new List<SavedDishDto> { CreateDish("Pizza") };

        // Act.
        var result = DishAutocompleteEngine.GetSuggestion("", dishes);

        // Assert.
        Assert.Null(result);
    }

    [Fact]
    public void GetSuggestion_NullDishList_ReturnsNull()
    {
        // Arrange.
        // Act.
        var result = DishAutocompleteEngine.GetSuggestion("Piz", null);

        // Assert.
        Assert.Null(result);
    }

    [Fact]
    public void GetSuggestion_EmptyDishList_ReturnsNull()
    {
        // Arrange.
        var dishes = new List<SavedDishDto>();

        // Act.
        var result = DishAutocompleteEngine.GetSuggestion("Piz", dishes);

        // Assert.
        Assert.Null(result);
    }

    [Fact]
    public void GetSuggestion_SingleCharPrefix_ReturnsRemainder()
    {
        // Arrange.
        var dishes = new List<SavedDishDto> { CreateDish("Pizza") };

        // Act.
        var result = DishAutocompleteEngine.GetSuggestion("P", dishes);

        // Assert.
        Assert.Equal("izza", result);
    }

    [Fact]
    public void GetSuggestion_UpperCaseInput_MatchesCaseInsensitive()
    {
        // Arrange.
        var dishes = new List<SavedDishDto> { CreateDish("pizza margherita") };

        // Act.
        var result = DishAutocompleteEngine.GetSuggestion("PIZZA", dishes);

        // Assert.
        Assert.Equal(" margherita", result);
    }

    [Fact]
    public void GetSuggestion_MixedCaseInput_MatchesCaseInsensitive()
    {
        // Arrange.
        var dishes = new List<SavedDishDto> { CreateDish("Spaghetti Bolognese") };

        // Act.
        var result = DishAutocompleteEngine.GetSuggestion("sPaGhEtTi", dishes);

        // Assert.
        Assert.Equal(" Bolognese", result);
    }

    [Fact]
    public void GetSuggestion_SpecialCharactersInDish_ReturnsRemainder()
    {
        // Arrange.
        var dishes = new List<SavedDishDto> { CreateDish("Mac & Cheese (homemade)") };

        // Act.
        var result = DishAutocompleteEngine.GetSuggestion("Mac", dishes);

        // Assert.
        Assert.Equal(" & Cheese (homemade)", result);
    }

    [Fact]
    public void GetSuggestion_ExactMatch_ReturnsNull()
    {
        // Arrange.
        var dishes = new List<SavedDishDto> { CreateDish("Pizza") };

        // Act.
        var result = DishAutocompleteEngine.GetSuggestion("Pizza", dishes);

        // Assert.
        Assert.Null(result);
    }

    [Fact]
    public void GetSuggestion_NoMatch_ReturnsNull()
    {
        // Arrange.
        var dishes = new List<SavedDishDto> { CreateDish("Pizza"), CreateDish("Pasta") };

        // Act.
        var result = DishAutocompleteEngine.GetSuggestion("Bur", dishes);

        // Assert.
        Assert.Null(result);
    }

    [Fact]
    public void ExtractActiveSegment_DelimiterAtEnd_ReturnsEmptyString()
    {
        // Arrange.
        var input = "Pizza & ";

        // Act.
        var result = DishAutocompleteEngine.ExtractActiveSegment(input);

        // Assert.
        Assert.Equal("", result);
    }

    [Fact]
    public void ExtractActiveSegment_NoDelimiter_ReturnsEntireInput()
    {
        // Arrange.
        var input = "Spaghetti Bolognese";

        // Act.
        var result = DishAutocompleteEngine.ExtractActiveSegment(input);

        // Assert.
        Assert.Equal("Spaghetti Bolognese", result);
    }

    [Fact]
    public void ExtractActiveSegment_MultipleDelimiters_ReturnsAfterLast()
    {
        // Arrange.
        var input = "Pizza & Pasta & Sal";

        // Act.
        var result = DishAutocompleteEngine.ExtractActiveSegment(input);

        // Assert.
        Assert.Equal("Sal", result);
    }

    [Fact]
    public void AcceptSuggestion_NoDelimiter_ReturnsMatchedDishName()
    {
        // Arrange.
        var input = "Piz";
        var matchedDishName = "Pizza";

        // Act.
        var result = DishAutocompleteEngine.AcceptSuggestion(input, matchedDishName);

        // Assert.
        Assert.Equal("Pizza", result);
    }

    [Fact]
    public void AcceptSuggestion_OneDelimiter_PreservesPreceding()
    {
        // Arrange.
        var input = "Pizza & Pas";
        var matchedDishName = "Pasta";

        // Act.
        var result = DishAutocompleteEngine.AcceptSuggestion(input, matchedDishName);

        // Assert.
        Assert.Equal("Pizza & Pasta", result);
    }

    [Fact]
    public void AcceptSuggestion_MultipleDelimiters_PreservesPreceding()
    {
        // Arrange.
        var input = "Pizza & Pasta & Sal";
        var matchedDishName = "Salad";

        // Act.
        var result = DishAutocompleteEngine.AcceptSuggestion(input, matchedDishName);

        // Assert.
        Assert.Equal("Pizza & Pasta & Salad", result);
    }

    private static SavedDishDto CreateDish(string description)
    {
        return new SavedDishDto(Guid.NewGuid(), description);
    }
}
