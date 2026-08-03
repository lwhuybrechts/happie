using System.Net;
using Bunit;
using Happie.Shared.Contracts;
using Happie.Web.Pages;
using Happie.Web.Resources;
using Happie.Web.Services;
using Happie.Web.Services.Caching;
using Happie.Web.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using RichardSzalay.MockHttp;
using MockHttp = RichardSzalay.MockHttp.MockHttpMessageHandler;

namespace Happie.Web.Tests.Pages;

public class SavedDishesPageTests : BunitContext
{
    private readonly Mock<ICachedApiClient> _cachedApiMock = new();
    private readonly Mock<IConnectivityService> _connectivityMock = new();
    private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();
    private readonly MockHttp _mockHttp = new();

    private static readonly Guid DishIdA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid DishIdB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly List<SavedDishDto> TestDishes =
    [
        new(DishIdA, "Pizza Margherita"),
        new(DishIdB, "Pasta Bolognese")
    ];

    public SavedDishesPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        SetupLocalizer();
        SetupCachedApiWithDishes(TestDishes);
        _connectivityMock.Setup(x => x.IsOnline).Returns(true);

        Services.AddSingleton(_cachedApiMock.Object);
        Services.AddSingleton(_connectivityMock.Object);
        Services.AddSingleton(_localizerMock.Object);

        // Register HttpClient for suggestions endpoint.
        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost/api/");
        Services.AddSingleton(httpClient);

        _mockHttp.When("/api/saved-dishes/suggestions")
            .Respond("application/json", "[]");
    }

    [Fact]
    public void Render_DishNames_RenderedAsClickableLinks()
    {
        // Act.
        var cut = Render<SavedDishesPage>();

        // Assert.
        var links = cut.FindAll("a.saved-dishes-page__name");
        Assert.Equal(2, links.Count);
        Assert.Equal("Pizza Margherita", links[0].TextContent);
        Assert.Equal($"/saved-dishes/{DishIdA}", links[0].GetAttribute("href"));
        Assert.Equal("Pasta Bolognese", links[1].TextContent);
        Assert.Equal($"/saved-dishes/{DishIdB}", links[1].GetAttribute("href"));
    }

    [Fact]
    public void Render_DishRows_NoInlineEditIconOrEditMode()
    {
        // Act.
        var cut = Render<SavedDishesPage>();

        // Assert — no edit icon buttons within dish rows.
        var editIcons = cut.FindAll(".saved-dishes-page__row .saved-dishes-page__icon-btn--edit");
        Assert.Empty(editIcons);

        // No inline edit inputs within dish rows.
        var editInputs = cut.FindAll(".saved-dishes-page__row input.saved-dishes-page__input");
        Assert.Empty(editInputs);
    }

    [Fact]
    public void Render_DishRows_StatisticsButtonPresent()
    {
        // Act.
        var cut = Render<SavedDishesPage>();

        // Assert — statistics buttons exist (one per dish).
        var rows = cut.FindAll(".saved-dishes-page__row");
        Assert.Equal(2, rows.Count);

        // Each row has a statistics icon button.
        foreach (var row in rows)
        {
            var actionsSection = row.QuerySelector(".saved-dishes-page__actions");
            Assert.NotNull(actionsSection);
            var iconButtons = actionsSection!.QuerySelectorAll(".saved-dishes-page__icon-btn");
            Assert.True(iconButtons.Length >= 1);
        }
    }

    [Fact]
    public void Render_DishRows_DeleteButtonPresent()
    {
        // Act.
        var cut = Render<SavedDishesPage>();

        // Assert — delete buttons exist (one per dish with the danger modifier).
        var deleteButtons = cut.FindAll(".saved-dishes-page__icon-btn--danger");
        Assert.Equal(2, deleteButtons.Count);
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
