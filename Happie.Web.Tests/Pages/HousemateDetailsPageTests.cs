using System.Net;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using Happie.Shared.Contracts;
using Happie.Web.Http;
using Happie.Web.Pages;
using Happie.Web.Resources;
using Happie.Web.Services;
using Happie.Web.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using RichardSzalay.MockHttp;

namespace Happie.Web.Tests.Pages;

public class HousemateDetailsPageTests : BunitContext
{
    private readonly Mock<IStatisticsApiClient> _statisticsApiMock = new();
    private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();
    private readonly RichardSzalay.MockHttp.MockHttpMessageHandler _mockHttp = new();
    private readonly FakeDelayService _fakeDelayService = new();
    private readonly LoadingIndicatorState _loadingIndicatorState;

    private static readonly Guid ValidHousemateId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public HousemateDetailsPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _loadingIndicatorState = new LoadingIndicatorState(_fakeDelayService);

        SetupLocalizer();

        Services.AddSingleton(_statisticsApiMock.Object);
        Services.AddSingleton(_localizerMock.Object);
        Services.AddSingleton(_loadingIndicatorState);

        // Register HttpClient with MockHttp for housemate list responses.
        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost/api/");
        Services.AddSingleton(httpClient);

        // Default timeline mock — returns empty timeline for any housemate.
        _statisticsApiMock
            .Setup(x => x.GetHousemateTimelineAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>()))
            .ReturnsAsync(new HousemateTimelineResponse(new List<HousemateTimelineDto>(), null));
    }

    [Fact]
    public void Render_InvalidGuidId_RedirectsToHousematesPage()
    {
        // Act.
        var cut = Render<HousemateDetailsPage>(parameters => parameters
            .Add(x => x.Id, "not-a-valid-guid"));

        // Assert.
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var bunitNav = (BunitNavigationManager)navigationManager;
        var lastNav = bunitNav.History.Last();
        Assert.Contains("/housemates", lastNav.Uri);
    }

    [Fact]
    public void Render_HousemateNotFoundInList_RedirectsToHousematesPage()
    {
        // Arrange — housemate list does not contain the requested ID.
        var housemates = new List<HousemateDto>
        {
            new(Guid.NewGuid(), "Alice", "#FF5733", 0),
        };

        SetupHousemateListResponse(housemates);

        // Act.
        var cut = Render<HousemateDetailsPage>(parameters => parameters
            .Add(x => x.Id, ValidHousemateId.ToString()));

        // Wait for redirect.
        cut.WaitForState(() =>
        {
            var navigationManager = Services.GetRequiredService<NavigationManager>();
            var bunitNav = (BunitNavigationManager)navigationManager;
            return bunitNav.History.Any(x => x.Uri.Contains("/housemates"));
        }, TimeSpan.FromSeconds(5));

        // Assert.
        var nav = Services.GetRequiredService<NavigationManager>();
        var bNav = (BunitNavigationManager)nav;
        var lastNav = bNav.History.Last();
        Assert.Contains("/housemates", lastNav.Uri);
    }

    [Fact]
    public void Render_ApiReturns404_RedirectsToHousematesPage()
    {
        // Arrange.
        SetupHousemateListResponse(new List<HousemateDto>
        {
            new(ValidHousemateId, "Bob", "#33FF57", 0),
        });

        _statisticsApiMock
            .Setup(x => x.GetHousemateStatisticsAsync(
                ValidHousemateId,
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>()))
            .ReturnsAsync((HousemateStatisticsResponse?)null);

        // Act.
        var cut = Render<HousemateDetailsPage>(parameters => parameters
            .Add(x => x.Id, ValidHousemateId.ToString()));

        // Wait for redirect.
        cut.WaitForState(() =>
        {
            var navigationManager = Services.GetRequiredService<NavigationManager>();
            var bunitNav = (BunitNavigationManager)navigationManager;
            return bunitNav.History.Any(x => x.Uri.Contains("/housemates"));
        }, TimeSpan.FromSeconds(5));

        // Assert.
        var nav = Services.GetRequiredService<NavigationManager>();
        var bNav = (BunitNavigationManager)nav;
        var lastNav = bNav.History.Last();
        Assert.Contains("/housemates", lastNav.Uri);
    }

    [Fact]
    public void Render_ZeroChefDaysInRange_DisplaysEmptyStateMessage()
    {
        // Arrange.
        SetupHousemateListResponse(new List<HousemateDto>
        {
            new(ValidHousemateId, "Bob", "#33FF57", 0),
        });

        var emptyStatistics = new HousemateStatisticsResponse(
            TimesCooked: 0,
            AllTimeTimesCooked: 0,
            DaysEatingIn: 0,
            CookRatioDays: 0,
            CookRatioEatingInDays: 0,
            LongestStreak: 0,
            BusiestWeek: 0,
            CookingShares: new List<CookingShareDto>(),
            TopDishes: new List<TopDishDto>());

        _statisticsApiMock
            .Setup(x => x.GetHousemateStatisticsAsync(
                ValidHousemateId,
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>()))
            .ReturnsAsync(emptyStatistics);

        // Act.
        var cut = Render<HousemateDetailsPage>(parameters => parameters
            .Add(x => x.Id, ValidHousemateId.ToString()));

        cut.WaitForState(() => cut.FindAll(".housemate-details-page__empty-state").Count > 0, TimeSpan.FromSeconds(5));

        // Assert.
        var emptyState = cut.Find(".housemate-details-page__empty-state");
        Assert.NotNull(emptyState);
        Assert.Equal("Stats_EmptyState", emptyState.TextContent);
    }

    [Fact]
    public void Render_WithCookingData_DisplaysHousemateNameAsHeading()
    {
        // Arrange.
        SetupHousemateListResponse(new List<HousemateDto>
        {
            new(ValidHousemateId, "Bob", "#33FF57", 0),
        });

        var statistics = CreateStatisticsWithData();

        _statisticsApiMock
            .Setup(x => x.GetHousemateStatisticsAsync(
                ValidHousemateId,
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>()))
            .ReturnsAsync(statistics);

        // Act.
        var cut = Render<HousemateDetailsPage>(parameters => parameters
            .Add(x => x.Id, ValidHousemateId.ToString()));

        cut.WaitForState(() => cut.FindAll(".housemate-details-page__title").Count > 0, TimeSpan.FromSeconds(5));

        // Assert.
        var title = cut.Find(".housemate-details-page__title");
        Assert.Equal("Bob", title.TextContent);
    }

    [Fact]
    public void Render_WithCookingData_DisplaysSummaryStatistics()
    {
        // Arrange.
        SetupHousemateListResponse(new List<HousemateDto>
        {
            new(ValidHousemateId, "Bob", "#33FF57", 0),
        });

        var statistics = CreateStatisticsWithData();

        _statisticsApiMock
            .Setup(x => x.GetHousemateStatisticsAsync(
                ValidHousemateId,
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>()))
            .ReturnsAsync(statistics);

        // Act.
        var cut = Render<HousemateDetailsPage>(parameters => parameters
            .Add(x => x.Id, ValidHousemateId.ToString()));

        cut.WaitForState(() => cut.FindAll(".housemate-details-page__summary-value").Count > 0, TimeSpan.FromSeconds(5));

        // Assert.
        var primaryValue = cut.Find(".housemate-details-page__summary-value");
        Assert.Equal("7", primaryValue.TextContent);
    }

    [Fact]
    public void Render_WithCookingData_IncludesTimeRangeSelector()
    {
        // Arrange.
        SetupHousemateListResponse(new List<HousemateDto>
        {
            new(ValidHousemateId, "Bob", "#33FF57", 0),
        });

        var statistics = CreateStatisticsWithData();

        _statisticsApiMock
            .Setup(x => x.GetHousemateStatisticsAsync(
                ValidHousemateId,
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>()))
            .ReturnsAsync(statistics);

        // Act.
        var cut = Render<HousemateDetailsPage>(parameters => parameters
            .Add(x => x.Id, ValidHousemateId.ToString()));

        cut.WaitForState(() => cut.FindAll(".time-range-selector").Count > 0, TimeSpan.FromSeconds(5));

        // Assert.
        var selector = cut.Find(".time-range-selector");
        Assert.NotNull(selector);

        // Default selection is 30 days (first pill active).
        var activePills = cut.FindAll(".time-range-selector__pill--active");
        Assert.Single(activePills);
    }

    private void SetupHousemateListResponse(List<HousemateDto> housemates)
    {
        var json = JsonSerializer.Serialize(housemates);
        _mockHttp.When("/api/housemates")
            .Respond("application/json", json);

        // Also respond to just "housemates" (relative path).
        _mockHttp.When("http://localhost/api/housemates")
            .Respond("application/json", json);
    }

    private static HousemateStatisticsResponse CreateStatisticsWithData()
    {
        return new HousemateStatisticsResponse(
            TimesCooked: 7,
            AllTimeTimesCooked: 20,
            DaysEatingIn: 15,
            CookRatioDays: 7,
            CookRatioEatingInDays: 15,
            LongestStreak: 3,
            BusiestWeek: 4,
            CookingShares: new List<CookingShareDto>
            {
                new(ValidHousemateId, "Bob", "#33FF57", 7),
                new(Guid.NewGuid(), "Alice", "#FF5733", 5),
            },
            TopDishes: new List<TopDishDto>
            {
                new(Guid.NewGuid(), "Pasta", 4),
                new(Guid.NewGuid(), "Rice", 3),
            });
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
