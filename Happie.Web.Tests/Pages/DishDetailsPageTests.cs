using System.Net;
using Bunit;
using Bunit.TestDoubles;
using Happie.Shared.Contracts;
using Happie.Web.Pages;
using Happie.Web.Resources;
using Happie.Web.Services;
using Happie.Web.Services.Caching;
using Happie.Web.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace Happie.Web.Tests.Pages;

public class DishDetailsPageTests : BunitContext
{
    private readonly Mock<ICachedApiClient> _cachedApiMock = new();
    private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();
    private readonly FakeDelayService _fakeDelayService = new();
    private readonly LoadingIndicatorState _loadingIndicatorState;

    private static readonly Guid ValidDishId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public DishDetailsPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _loadingIndicatorState = new LoadingIndicatorState(_fakeDelayService);

        SetupLocalizer();

        Services.AddSingleton(_cachedApiMock.Object);
        Services.AddSingleton(_localizerMock.Object);
        Services.AddSingleton(_loadingIndicatorState);

        // Register HttpClient needed by DishDetailsPage and child panels.
        this.RegisterHttpClient(HttpStatusCode.OK, new RecipeSummaryResponse(null, null, null));
    }

    [Fact]
    public void Render_WithValidDish_DisplaysDishNameAsHeading()
    {
        // Arrange.
        SetupSavedDishesCache(ValidDishId, "Spaghetti Bolognese");

        // Act.
        var cut = Render<DishDetailsPage>(parameters => parameters
            .Add(x => x.Id, ValidDishId.ToString()));

        cut.WaitForState(() => cut.FindAll(".dish-details-page__title").Count > 0, TimeSpan.FromSeconds(5));

        // Assert.
        var title = cut.Find(".dish-details-page__title");
        Assert.Equal("Spaghetti Bolognese", title.TextContent);
    }

    [Fact]
    public void Render_WhileLoading_DoesNotDisplayPageContent()
    {
        // Arrange — set up a cached API that never completes so we stay in loading state.
        var tcs = new TaskCompletionSource<SavedDishesFetchResult>();
        _cachedApiMock
            .Setup(x => x.GetSavedDishesAsync())
            .Returns(tcs.Task);

        // Act.
        var cut = Render<DishDetailsPage>(parameters => parameters
            .Add(x => x.Id, ValidDishId.ToString()));

        // Assert — the page header and title should not be rendered while loading.
        Assert.Empty(cut.FindAll(".dish-details-page__title"));
        Assert.Empty(cut.FindAll(".dish-details-page__header"));

        // Clean up.
        tcs.SetResult(new SavedDishesFetchResult(new List<SavedDishDto>(), false, false));
    }

    [Fact]
    public void Render_InvalidGuidId_NavigatesToSavedDishesPage()
    {
        // Act.
        var cut = Render<DishDetailsPage>(parameters => parameters
            .Add(x => x.Id, "not-a-guid"));

        // Assert.
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var bunitNav = (BunitNavigationManager)navigationManager;
        var lastNav = bunitNav.History.Last();
        Assert.Contains("/saved-dishes", lastNav.Uri);
    }

    [Fact]
    public void Render_DishNotFound_NavigatesToSavedDishesPage()
    {
        // Arrange — return dishes that do not include the requested ID.
        var otherDishId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var dishes = new List<SavedDishDto> { new(otherDishId, "Other Dish") };
        _cachedApiMock
            .Setup(x => x.GetSavedDishesAsync())
            .ReturnsAsync(new SavedDishesFetchResult(dishes, false, false));

        // Act.
        var cut = Render<DishDetailsPage>(parameters => parameters
            .Add(x => x.Id, ValidDishId.ToString()));

        cut.WaitForState(() =>
        {
            var navigationManager = Services.GetRequiredService<NavigationManager>();
            var bunitNav = (BunitNavigationManager)navigationManager;
            return bunitNav.History.Any(x => x.Uri.Contains("/saved-dishes"));
        }, TimeSpan.FromSeconds(5));

        // Assert.
        var nav = Services.GetRequiredService<NavigationManager>();
        var bNav = (BunitNavigationManager)nav;
        var lastNav = bNav.History.Last();
        Assert.Contains("/saved-dishes", lastNav.Uri);
    }

    [Fact]
    public void Render_WithValidDish_StatsButtonNavigatesToStats()
    {
        // Arrange.
        SetupSavedDishesCache(ValidDishId, "Pasta Carbonara");

        // Act.
        var cut = Render<DishDetailsPage>(parameters => parameters
            .Add(x => x.Id, ValidDishId.ToString()));

        cut.WaitForState(() => cut.FindAll(".dish-details-page__stats-btn").Count > 0, TimeSpan.FromSeconds(5));

        var statsButton = cut.Find(".dish-details-page__stats-btn");
        statsButton.Click();

        // Assert.
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var bunitNav = (BunitNavigationManager)navigationManager;
        var lastNav = bunitNav.History.Last();
        Assert.Contains($"/saved-dishes/{ValidDishId}/stats", lastNav.Uri);
    }

    [Fact]
    public void ClickEditIcon_ShowsNameInputField()
    {
        // Arrange.
        SetupSavedDishesCache(ValidDishId, "Risotto");

        var cut = Render<DishDetailsPage>(parameters => parameters
            .Add(x => x.Id, ValidDishId.ToString()));

        cut.WaitForState(() => cut.FindAll(".dish-details-page__title").Count > 0, TimeSpan.FromSeconds(5));

        // Act — click the edit icon.
        var editButton = cut.FindAll(".dish-details-page__icon-btn")
            .First(x => x.GetAttribute("aria-label") == "DishDetails_EditName");
        editButton.Click();

        // Assert — input field should appear.
        var nameInput = cut.Find(".dish-details-page__name-input");
        Assert.NotNull(nameInput);
        Assert.Equal("Risotto", nameInput.GetAttribute("value"));
    }

    [Fact]
    public void ClickDiscardButton_RevertsToReadMode()
    {
        // Arrange.
        SetupSavedDishesCache(ValidDishId, "Risotto");

        var cut = Render<DishDetailsPage>(parameters => parameters
            .Add(x => x.Id, ValidDishId.ToString()));

        cut.WaitForState(() => cut.FindAll(".dish-details-page__title").Count > 0, TimeSpan.FromSeconds(5));

        // Enter edit mode.
        var editButton = cut.FindAll(".dish-details-page__icon-btn")
            .First(x => x.GetAttribute("aria-label") == "DishDetails_EditName");
        editButton.Click();

        // Act — click discard.
        var discardButton = cut.Find(".dish-details-page__icon-btn--discard");
        discardButton.Click();

        // Assert — title should be visible again, input should not be present.
        var title = cut.Find(".dish-details-page__title");
        Assert.Equal("Risotto", title.TextContent);
        Assert.Empty(cut.FindAll(".dish-details-page__name-input"));
    }

    [Fact]
    public void NameInput_BlocksAmpersandCharacter()
    {
        // Arrange.
        SetupSavedDishesCache(ValidDishId, "Risotto");

        var cut = Render<DishDetailsPage>(parameters => parameters
            .Add(x => x.Id, ValidDishId.ToString()));

        cut.WaitForState(() => cut.FindAll(".dish-details-page__title").Count > 0, TimeSpan.FromSeconds(5));

        // Enter edit mode.
        var editButton = cut.FindAll(".dish-details-page__icon-btn")
            .First(x => x.GetAttribute("aria-label") == "DishDetails_EditName");
        editButton.Click();

        // Act — type text containing '&'.
        var nameInput = cut.Find(".dish-details-page__name-input");
        nameInput.Input("Rice & Beans");

        // Assert — the '&' should be stripped from the input value.
        var updatedInput = cut.Find(".dish-details-page__name-input");
        Assert.Equal("Rice  Beans", updatedInput.GetAttribute("value"));
    }

    private void SetupSavedDishesCache(Guid dishId, string description)
    {
        var dishes = new List<SavedDishDto> { new(dishId, description) };
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
