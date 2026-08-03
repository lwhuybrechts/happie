using System.Net;
using System.Net.Http.Json;
using Bunit;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;
using Happie.Web.Components;
using Happie.Web.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using RichardSzalay.MockHttp;

namespace Happie.Web.Tests.Components;

public class IngredientsPanelTests : BunitContext
{
    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();

    private static readonly Guid TestDishId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Ingredient1Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Ingredient2Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid Ingredient3Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    public IngredientsPanelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        SetupLocalizer();

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost/api/");
        Services.AddSingleton(httpClient);
        Services.AddSingleton(_localizerMock.Object);
    }

    [Fact]
    public void Render_NoIngredients_DisplaysPlaceholderText()
    {
        // Arrange.
        SetupIngredientsEndpoint(new List<IngredientDto>(), new List<IngredientCheckDto>());

        // Act.
        var cut = Render<IngredientsPanel>(parameters => parameters
            .Add(x => x.SavedDishId, TestDishId)
            .Add(x => x.BaseServings, 4));

        cut.WaitForState(() => cut.FindAll(".ingredients-panel__empty").Count > 0, TimeSpan.FromSeconds(5));

        // Assert.
        var placeholder = cut.Find(".ingredients-panel__empty");
        Assert.Equal("DishDetails_IngredientsEmpty", placeholder.TextContent);
    }

    [Fact]
    public void Render_WithIngredients_DisplaysRowsWithCheckboxAmountAndName()
    {
        // Arrange.
        var ingredients = new List<IngredientDto>
        {
            new(Ingredient1Id, 200, UnitOfMeasurement.G, "Pasta", 0),
            new(Ingredient2Id, 2, UnitOfMeasurement.Piece, "Tomatoes", 1),
        };
        var checks = new List<IngredientCheckDto>
        {
            new(Ingredient1Id, true),
        };
        SetupIngredientsEndpoint(ingredients, checks);

        // Act.
        var cut = Render<IngredientsPanel>(parameters => parameters
            .Add(x => x.SavedDishId, TestDishId)
            .Add(x => x.BaseServings, 4));

        cut.WaitForState(() => cut.FindAll(".ingredients-panel__row").Count > 0, TimeSpan.FromSeconds(5));

        // Assert.
        var rows = cut.FindAll(".ingredients-panel__row");
        Assert.Equal(2, rows.Count);

        // First row has checkbox, amount, and name.
        var checkboxes = cut.FindAll(".ingredients-panel__checkbox");
        Assert.Equal(2, checkboxes.Count);

        var names = cut.FindAll(".ingredients-panel__name");
        Assert.Equal("Pasta", names[0].TextContent);
        Assert.Equal("Tomatoes", names[1].TextContent);
    }

    [Fact]
    public void PortionScaling_PlusButton_IncrementsServingsAndRecalculatesAmounts()
    {
        // Arrange.
        var ingredients = new List<IngredientDto>
        {
            new(Ingredient1Id, 200, UnitOfMeasurement.G, "Flour", 0),
        };
        SetupIngredientsEndpoint(ingredients, new List<IngredientCheckDto>());

        var cut = Render<IngredientsPanel>(parameters => parameters
            .Add(x => x.SavedDishId, TestDishId)
            .Add(x => x.BaseServings, 4));

        cut.WaitForState(() => cut.FindAll(".ingredients-panel__row").Count > 0, TimeSpan.FromSeconds(5));

        // Act — click plus button to increase servings from 4 to 5.
        var plusButton = cut.Find("[aria-label='Increase servings']");
        plusButton.Click();

        // Assert — amount should be scaled from 200g (4 servings) to 250g (5 servings).
        // The decimal separator is locale-dependent so check for both dot and comma formats.
        var amount = cut.Find(".ingredients-panel__amount");
        Assert.True(
            amount.TextContent.Contains("250.00") || amount.TextContent.Contains("250,00"),
            $"Expected amount to contain '250.00' or '250,00' but was '{amount.TextContent}'");
    }

    [Fact]
    public void Render_BaseServingsNull_HidesServingsControls()
    {
        // Arrange.
        var ingredients = new List<IngredientDto>
        {
            new(Ingredient1Id, 100, UnitOfMeasurement.G, "Sugar", 0),
        };
        SetupIngredientsEndpoint(ingredients, new List<IngredientCheckDto>());

        // Act.
        var cut = Render<IngredientsPanel>(parameters => parameters
            .Add(x => x.SavedDishId, TestDishId)
            .Add(x => x.BaseServings, (int?)null));

        cut.WaitForState(() => cut.FindAll(".ingredients-panel__row").Count > 0, TimeSpan.FromSeconds(5));

        // Assert — no minus/plus buttons and no servings label.
        Assert.Empty(cut.FindAll("[aria-label='Increase servings']"));
        Assert.Empty(cut.FindAll("[aria-label='Decrease servings']"));
        Assert.Empty(cut.FindAll(".ingredients-panel__servings"));
    }

    [Fact]
    public void CheckAllButton_LabelChangesBasedOnThreshold()
    {
        // Arrange — 3 ingredients, 0 checked → "Check all" label.
        var ingredients = new List<IngredientDto>
        {
            new(Ingredient1Id, 1, UnitOfMeasurement.Piece, "Egg", 0),
            new(Ingredient2Id, 2, UnitOfMeasurement.Piece, "Apple", 1),
            new(Ingredient3Id, 3, UnitOfMeasurement.Piece, "Banana", 2),
        };
        SetupIngredientsEndpoint(ingredients, new List<IngredientCheckDto>());

        // Setup PUT responses for checkbox toggles.
        _mockHttp
            .When(HttpMethod.Put, "http://localhost/api/saved-dishes/*/ingredients/*/check")
            .Respond(HttpStatusCode.OK);

        var cut = Render<IngredientsPanel>(parameters => parameters
            .Add(x => x.SavedDishId, TestDishId)
            .Add(x => x.BaseServings, 2));

        cut.WaitForState(() => cut.FindAll(".ingredients-panel__toggle-btn").Count > 0, TimeSpan.FromSeconds(5));

        // Assert — 0 of 3 checked (0% ≤ 50%) → "Check all".
        var toggleButton = cut.Find(".ingredients-panel__toggle-btn");
        Assert.Equal("DishDetails_CheckAll", toggleButton.TextContent);
    }

    [Fact]
    public void CheckAllButton_WhenMostChecked_ShowsUncheckAll()
    {
        // Arrange — 3 ingredients, all 3 checked (100% > 50%) → "Uncheck all".
        var ingredients = new List<IngredientDto>
        {
            new(Ingredient1Id, 1, UnitOfMeasurement.Piece, "Egg", 0),
            new(Ingredient2Id, 2, UnitOfMeasurement.Piece, "Apple", 1),
            new(Ingredient3Id, 3, UnitOfMeasurement.Piece, "Banana", 2),
        };
        var checks = new List<IngredientCheckDto>
        {
            new(Ingredient1Id, true),
            new(Ingredient2Id, true),
            new(Ingredient3Id, true),
        };
        SetupIngredientsEndpoint(ingredients, checks);

        var cut = Render<IngredientsPanel>(parameters => parameters
            .Add(x => x.SavedDishId, TestDishId)
            .Add(x => x.BaseServings, 2));

        cut.WaitForState(() => cut.FindAll(".ingredients-panel__toggle-btn").Count > 0, TimeSpan.FromSeconds(5));

        // Assert — all checked (100% > 50%) → "Uncheck all".
        var toggleButton = cut.Find(".ingredients-panel__toggle-btn");
        Assert.Equal("DishDetails_UncheckAll", toggleButton.TextContent);
    }

    private void SetupIngredientsEndpoint(List<IngredientDto> ingredients, List<IngredientCheckDto> checks)
    {
        var response = new IngredientsResponse(ingredients, checks);
        _mockHttp
            .When(HttpMethod.Get, "http://localhost/api/saved-dishes/*/ingredients")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(response));
    }

    private void SetupLocalizer()
    {
        _localizerMock
            .Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        _localizerMock
            .Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] arguments) => new LocalizedString(key, key));
    }
}
