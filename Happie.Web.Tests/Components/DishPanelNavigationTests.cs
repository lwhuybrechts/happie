using Bunit;
using Happie.Shared.Contracts;
using Happie.Web.Components;
using Happie.Web.Resources;
using Happie.Web.Services.Caching;
using Happie.Web.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace Happie.Web.Tests.Components;

public class DishPanelNavigationTests : BunitContext
{
    private readonly Mock<ICachedApiClient> _cachedApiMock = new();
    private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();

    private static readonly Guid DishIdA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid DishIdB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid UnresolvedDishId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static readonly List<SavedDishDto> TestDishes =
    [
        new(DishIdA, "Pizza Margherita"),
        new(DishIdB, "Pasta Bolognese")
    ];

    public DishPanelNavigationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        SetupLocalizer();
        SetupCachedApiWithDishes(TestDishes);

        Services.AddSingleton(_cachedApiMock.Object);
        Services.AddSingleton(_localizerMock.Object);
    }

    [Fact]
    public void Render_WithSingleSavedDish_RendersClickableLinkToDetails()
    {
        // Arrange & Act.
        var cut = RenderDishPanelWithSavedDishes(
            "Pizza Margherita",
            new List<Guid> { DishIdA });

        // Assert.
        var link = cut.Find("a.dish-panel__dish-link");
        Assert.Equal("Pizza Margherita", link.TextContent);
        Assert.Equal($"/saved-dishes/{DishIdA}", link.GetAttribute("href"));
    }

    [Fact]
    public void Render_WithMultipleSavedDishes_RendersLinksWithSeparators()
    {
        // Arrange & Act.
        var cut = RenderDishPanelWithSavedDishes(
            "Pizza Margherita & Pasta Bolognese",
            new List<Guid> { DishIdA, DishIdB });

        // Assert.
        var links = cut.FindAll("a.dish-panel__dish-link");
        Assert.Equal(2, links.Count);
        Assert.Equal("Pizza Margherita", links[0].TextContent);
        Assert.Equal($"/saved-dishes/{DishIdA}", links[0].GetAttribute("href"));
        Assert.Equal("Pasta Bolognese", links[1].TextContent);
        Assert.Equal($"/saved-dishes/{DishIdB}", links[1].GetAttribute("href"));

        // Verify separator exists between the two links.
        var separators = cut.FindAll("span.dish-panel__separator");
        Assert.Single(separators);
        Assert.Contains("&", separators[0].TextContent);
    }

    [Fact]
    public void Render_WithUnresolvedDishId_OmitsUnresolvedFromLinks()
    {
        // Arrange & Act — include an ID that does not resolve to any saved dish.
        var cut = RenderDishPanelWithSavedDishes(
            "Pizza Margherita",
            new List<Guid> { DishIdA, UnresolvedDishId });

        // Assert — only the resolved dish is rendered as a link.
        var links = cut.FindAll("a.dish-panel__dish-link");
        Assert.Single(links);
        Assert.Equal("Pizza Margherita", links[0].TextContent);
        Assert.Equal($"/saved-dishes/{DishIdA}", links[0].GetAttribute("href"));
    }

    [Fact]
    public void Render_WithSavedDishLinks_AppliesHoverStyleClass()
    {
        // Arrange & Act.
        var cut = RenderDishPanelWithSavedDishes(
            "Pizza Margherita",
            new List<Guid> { DishIdA });

        // Assert — the link has the dish-panel__dish-link class which includes :hover underline in CSS.
        var link = cut.Find("a.dish-panel__dish-link");
        Assert.Contains("dish-panel__dish-link", link.GetAttribute("class"));
    }

    private IRenderedComponent<DishPanel> RenderDishPanelWithSavedDishes(
        string description,
        List<Guid> savedDishIds)
    {
        return Render<DishPanel>(parameters => parameters
            .Add(x => x.Date, "2025-01-15")
            .Add(x => x.Dish, new DishDto(description, null, null, null, null, savedDishIds))
            .Add(x => x.Attendance, new List<AttendanceDto>())
            .Add(x => x.ActingHousemateId, Guid.NewGuid())
            .Add(x => x.OnDishChanged, EventCallback.Factory.Create<string?>(this, _ => { }))
            .Add(x => x.SavedDishModalRef, null));
    }

    private void SetupCachedApiWithDishes(List<SavedDishDto> dishes)
    {
        _cachedApiMock
            .Setup(x => x.GetSavedDishesAsync())
            .ReturnsAsync(new SavedDishesFetchResult(dishes, false, false));
    }

    private void SetupLocalizer()
    {
        _localizerMock
            .Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        _localizerMock
            .Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] _) => new LocalizedString(key, key));
    }
}
