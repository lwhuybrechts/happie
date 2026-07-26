using Bunit;
using Bunit.TestDoubles;
using Happie.Shared.Contracts;
using Happie.Web.Http;
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
    private readonly Mock<IStatisticsApiClient> _statisticsApiMock = new();
    private readonly Mock<ICachedApiClient> _cachedApiMock = new();
    private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();
    private readonly FakeDelayService _fakeDelayService = new();
    private readonly LoadingIndicatorState _loadingIndicatorState;

    private static readonly Guid ValidDishId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public DishDetailsPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _loadingIndicatorState = new LoadingIndicatorState(_fakeDelayService);

        SetupLocalizer();

        Services.AddSingleton(_statisticsApiMock.Object);
        Services.AddSingleton(_cachedApiMock.Object);
        Services.AddSingleton(_localizerMock.Object);
        Services.AddSingleton(_loadingIndicatorState);

        // Register HttpClient needed by the page for housemate list fetches.
        this.RegisterHttpClient(System.Net.HttpStatusCode.OK, new List<HousemateDto>());

        // Default timeline mock — returns empty timeline for any dish.
        _statisticsApiMock
            .Setup(x => x.GetDishTimelineAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>()))
            .ReturnsAsync(new DishTimelineResponse(new List<DishTimelineDto>(), null));
    }

    [Fact]
    public void Render_InvalidGuidId_RedirectsToSavedDishesPage()
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
    public void Render_ApiReturns404_RedirectsToSavedDishesPage()
    {
        // Arrange.
        SetupSavedDishesCache(ValidDishId, "Test Dish");

        _statisticsApiMock
            .Setup(x => x.GetDishStatisticsAsync(
                ValidDishId,
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>()))
            .ReturnsAsync((DishStatisticsResponse?)null);

        // Act.
        var cut = Render<DishDetailsPage>(parameters => parameters
            .Add(x => x.Id, ValidDishId.ToString()));

        // Wait for async load to complete.
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
    public void Render_ZeroCookingDaysInRange_DisplaysEmptyStateMessage()
    {
        // Arrange.
        SetupSavedDishesCache(ValidDishId, "Pasta Carbonara");

        var emptyStatistics = new DishStatisticsResponse(
            TimesCooked: 0,
            AllTimeTimesCooked: 0,
            LastCookedDate: null,
            FirstCookedDate: null,
            CookingShares: new List<CookingShareDto>());

        _statisticsApiMock
            .Setup(x => x.GetDishStatisticsAsync(
                ValidDishId,
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>()))
            .ReturnsAsync(emptyStatistics);

        // Act.
        var cut = Render<DishDetailsPage>(parameters => parameters
            .Add(x => x.Id, ValidDishId.ToString()));

        cut.WaitForState(() => cut.FindAll(".dish-details-page__empty-state").Count > 0, TimeSpan.FromSeconds(5));

        // Assert.
        var emptyState = cut.Find(".dish-details-page__empty-message");
        Assert.NotNull(emptyState);
        Assert.Equal("Stats_EmptyState", emptyState.TextContent);
    }

    [Fact]
    public void Render_WithCookingData_DisplaysSummaryStatistics()
    {
        // Arrange.
        SetupSavedDishesCache(ValidDishId, "Pasta Carbonara");

        var statistics = new DishStatisticsResponse(
            TimesCooked: 5,
            AllTimeTimesCooked: 12,
            LastCookedDate: DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"),
            FirstCookedDate: null,
            CookingShares: new List<CookingShareDto>());

        _statisticsApiMock
            .Setup(x => x.GetDishStatisticsAsync(
                ValidDishId,
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>()))
            .ReturnsAsync(statistics);

        // Act.
        var cut = Render<DishDetailsPage>(parameters => parameters
            .Add(x => x.Id, ValidDishId.ToString()));

        cut.WaitForState(() => cut.FindAll(".dish-details-page__primary-count").Count > 0, TimeSpan.FromSeconds(5));

        // Assert.
        var primaryCount = cut.Find(".dish-details-page__primary-count");
        Assert.Equal("5", primaryCount.TextContent);
    }

    [Fact]
    public void Render_WithValidDish_DisplaysDishDescriptionAsHeading()
    {
        // Arrange.
        SetupSavedDishesCache(ValidDishId, "Pasta Carbonara");

        var statistics = new DishStatisticsResponse(
            TimesCooked: 3,
            AllTimeTimesCooked: 10,
            LastCookedDate: null,
            FirstCookedDate: null,
            CookingShares: new List<CookingShareDto>());

        _statisticsApiMock
            .Setup(x => x.GetDishStatisticsAsync(
                ValidDishId,
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>()))
            .ReturnsAsync(statistics);

        // Act.
        var cut = Render<DishDetailsPage>(parameters => parameters
            .Add(x => x.Id, ValidDishId.ToString()));

        cut.WaitForState(() => cut.FindAll(".dish-details-page__title").Count > 0, TimeSpan.FromSeconds(5));

        // Assert.
        var title = cut.Find(".dish-details-page__title");
        Assert.Equal("Pasta Carbonara", title.TextContent);
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
