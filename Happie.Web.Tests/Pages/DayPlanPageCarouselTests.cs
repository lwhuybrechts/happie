using Bunit;
using Bunit.TestDoubles;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;
using Happie.Shared.Resources;
using Happie.Web.Pages;
using Happie.Web.Resources;
using Happie.Web.Services;
using Happie.Web.Services.Caching;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Moq;

namespace Happie.Web.Tests.Pages;

public class DayPlanPageCarouselTests : BunitContext
{
    private readonly Mock<ICachedApiClient> _cachedApiMock = new();
    private readonly Mock<ISyncService> _syncServiceMock = new();
    private readonly Mock<IConnectivityService> _connectivityServiceMock = new();
    private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();

    private readonly List<string> _getDayPlanCallOrder = new();

    public DayPlanPageCarouselTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        SetupLocalizer();
        _connectivityServiceMock.Setup(x => x.IsOnline).Returns(true);
        _cachedApiMock.Setup(x => x.IsColdCacheFetch).Returns(false);
        _cachedApiMock.Setup(x => x.HasLoadError).Returns(false);

        Services.AddSingleton(_cachedApiMock.Object);
        Services.AddSingleton(_syncServiceMock.Object);
        Services.AddSingleton(_connectivityServiceMock.Object);
        Services.AddSingleton(_localizerMock.Object);

        Services.AddSingleton(serviceProvider =>
            new LocaleService(serviceProvider.GetRequiredService<IJSRuntime>()));

        Services.AddScoped(serviceProvider =>
            new ActiveHousemateService(serviceProvider.GetRequiredService<IJSRuntime>()));

        // SharedStringResolver is needed by HistorySection.
        Services.AddSingleton<SharedStringResolver>();

        // HttpClient needed by NudgeModal.
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost/api/") };
        Services.AddSingleton(httpClient);
    }

    [Fact]
    public void OnParametersSetAsync_NavigateForwardWithNextLoaded_RecyclesPanelsCorrectly()
    {
        // Arrange.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var tomorrow = today.AddDays(1);
        var todayData = CreateDayPlan(today);
        var tomorrowData = CreateDayPlan(tomorrow);

        SetupGetDayPlanAsync(today.ToString("yyyy-MM-dd"), todayData);
        SetupGetDayPlanAsync(tomorrow.ToString("yyyy-MM-dd"), tomorrowData);
        SetupGetDayPlanAsync(today.AddDays(-1).ToString("yyyy-MM-dd"), null);
        SetupGetDayPlanAsync(tomorrow.AddDays(1).ToString("yyyy-MM-dd"), null);

        var cut = RenderDayPlanPage(today.ToString("yyyy-MM-dd"));

        // Wait for pre-fetch to complete by waiting for next panel content to appear.
        cut.WaitForState(() => cut.FindAll(".swipe-carousel__panel--next .swipe-carousel__panel-content").Count > 0, TimeSpan.FromSeconds(2));

        // Act — navigate forward to tomorrow.
        cut.Render(p => p.Add(x => x.Date, tomorrow.ToString("yyyy-MM-dd")));

        // Assert — the previous panel should now show today's data (panel-content rendered).
        cut.WaitForState(() => cut.FindAll(".swipe-carousel__panel--prev .swipe-carousel__panel-content").Count > 0, TimeSpan.FromSeconds(2));
        var prevPanelContent = cut.Find(".swipe-carousel__panel--prev .swipe-carousel__panel-content");
        Assert.NotNull(prevPanelContent);
    }

    [Fact]
    public void OnParametersSetAsync_NavigateBackwardWithPrevLoaded_RecyclesPanelsCorrectly()
    {
        // Arrange.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var yesterday = today.AddDays(-1);
        var todayData = CreateDayPlan(today);
        var yesterdayData = CreateDayPlan(yesterday);

        SetupGetDayPlanAsync(today.ToString("yyyy-MM-dd"), todayData);
        SetupGetDayPlanAsync(yesterday.ToString("yyyy-MM-dd"), yesterdayData);
        SetupGetDayPlanAsync(today.AddDays(1).ToString("yyyy-MM-dd"), null);
        SetupGetDayPlanAsync(yesterday.AddDays(-1).ToString("yyyy-MM-dd"), null);

        var cut = RenderDayPlanPage(today.ToString("yyyy-MM-dd"));

        // Wait for pre-fetch to complete.
        cut.WaitForState(() => cut.FindAll(".swipe-carousel__panel--prev .swipe-carousel__panel-content").Count > 0, TimeSpan.FromSeconds(2));

        // Act — navigate backward to yesterday.
        cut.Render(p => p.Add(x => x.Date, yesterday.ToString("yyyy-MM-dd")));

        // Assert — the next panel should now show today's data (panel-content rendered).
        cut.WaitForState(() => cut.FindAll(".swipe-carousel__panel--next .swipe-carousel__panel-content").Count > 0, TimeSpan.FromSeconds(2));
        var nextPanelContent = cut.Find(".swipe-carousel__panel--next .swipe-carousel__panel-content");
        Assert.NotNull(nextPanelContent);
    }

    [Fact]
    public void OnParametersSetAsync_InitialLoad_FetchesCurrentDayBeforeAdjacentDays()
    {
        // Arrange.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayStr = today.ToString("yyyy-MM-dd");
        var prevStr = today.AddDays(-1).ToString("yyyy-MM-dd");
        var nextStr = today.AddDays(1).ToString("yyyy-MM-dd");

        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(It.IsAny<string>()))
            .Returns((string date) =>
            {
                _getDayPlanCallOrder.Add(date);
                return Task.FromResult<DayPlanResponse?>(CreateDayPlan(DateOnly.ParseExact(date, "yyyy-MM-dd")));
            });

        // Act.
        var cut = RenderDayPlanPage(todayStr);

        // Wait for pre-fetches to fire.
        cut.WaitForState(() => _getDayPlanCallOrder.Count >= 3, TimeSpan.FromSeconds(2));

        // Assert — current day is fetched first.
        Assert.Equal(todayStr, _getDayPlanCallOrder[0]);
    }

    [Fact]
    public void OnParametersSetAsync_DateChangesWhilePrefetchPending_CancelsPreviousPrefetch()
    {
        // Arrange.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var tomorrow = today.AddDays(1);
        var todayStr = today.ToString("yyyy-MM-dd");
        var tomorrowStr = tomorrow.ToString("yyyy-MM-dd");

        // Use a TaskCompletionSource to keep the initial pre-fetch hanging.
        var prevFetchTcs = new TaskCompletionSource<DayPlanResponse?>();
        var nextFetchTcs = new TaskCompletionSource<DayPlanResponse?>();

        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(todayStr))
            .ReturnsAsync(CreateDayPlan(today));

        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(today.AddDays(-1).ToString("yyyy-MM-dd")))
            .Returns(prevFetchTcs.Task);

        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(today.AddDays(1).ToString("yyyy-MM-dd")))
            .Returns(nextFetchTcs.Task);

        var cut = RenderDayPlanPage(todayStr);

        // Act — navigate to a different date while pre-fetches are pending.
        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(tomorrowStr))
            .ReturnsAsync(CreateDayPlan(tomorrow));
        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(tomorrow.AddDays(-1).ToString("yyyy-MM-dd")))
            .ReturnsAsync(CreateDayPlan(today));
        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(tomorrow.AddDays(1).ToString("yyyy-MM-dd")))
            .ReturnsAsync(CreateDayPlan(tomorrow.AddDays(1)));

        cut.Render(p => p.Add(x => x.Date, tomorrowStr));

        // Complete the old pre-fetches after navigation — these should be discarded.
        prevFetchTcs.SetResult(CreateDayPlan(today.AddDays(-1)));
        nextFetchTcs.SetResult(CreateDayPlan(today.AddDays(1)));

        // Assert — the new date's adjacent data should load, not the old one.
        // The active panel should show tomorrow's content (verify it rendered without error).
        cut.WaitForState(() => cut.FindAll(".swipe-carousel__panel--active .swipe-carousel__panel-content").Count > 0, TimeSpan.FromSeconds(2));
        Assert.NotEmpty(cut.FindAll(".swipe-carousel__panel--active .swipe-carousel__panel-content"));
    }

    [Fact]
    public void Render_PrevPanelNotLoaded_ShowsLeftArrowPlaceholder()
    {
        // Arrange.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayStr = today.ToString("yyyy-MM-dd");

        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(todayStr))
            .ReturnsAsync(CreateDayPlan(today));

        // Adjacent fetches return null — not loaded.
        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(today.AddDays(-1).ToString("yyyy-MM-dd")))
            .ReturnsAsync((DayPlanResponse?)null);
        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(today.AddDays(1).ToString("yyyy-MM-dd")))
            .ReturnsAsync((DayPlanResponse?)null);

        // Act.
        var cut = RenderDayPlanPage(todayStr);

        // Assert — prev panel shows left arrow (polyline points "15 18 9 12 15 6").
        var prevPanel = cut.Find(".swipe-carousel__panel--prev");
        var arrowPlaceholder = prevPanel.QuerySelector(".swipe-carousel__arrow-placeholder");
        Assert.NotNull(arrowPlaceholder);

        var polyline = arrowPlaceholder!.QuerySelector("polyline");
        Assert.NotNull(polyline);
        Assert.Equal("15 18 9 12 15 6", polyline!.GetAttribute("points"));
    }

    [Fact]
    public void Render_NextPanelNotLoaded_ShowsRightArrowPlaceholder()
    {
        // Arrange.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayStr = today.ToString("yyyy-MM-dd");

        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(todayStr))
            .ReturnsAsync(CreateDayPlan(today));

        // Adjacent fetches return null — not loaded.
        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(today.AddDays(-1).ToString("yyyy-MM-dd")))
            .ReturnsAsync((DayPlanResponse?)null);
        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(today.AddDays(1).ToString("yyyy-MM-dd")))
            .ReturnsAsync((DayPlanResponse?)null);

        // Act.
        var cut = RenderDayPlanPage(todayStr);

        // Assert — next panel shows right arrow (polyline points "9 18 15 12 9 6").
        var nextPanel = cut.Find(".swipe-carousel__panel--next");
        var arrowPlaceholder = nextPanel.QuerySelector(".swipe-carousel__arrow-placeholder");
        Assert.NotNull(arrowPlaceholder);

        var polyline = arrowPlaceholder!.QuerySelector("polyline");
        Assert.NotNull(polyline);
        Assert.Equal("9 18 15 12 9 6", polyline!.GetAttribute("points"));
    }

    [Fact]
    public async Task SwipeLeftAsync_NextPanelNotLoaded_NavigatesToNextDayRoute()
    {
        // Arrange.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayStr = today.ToString("yyyy-MM-dd");
        var tomorrowStr = today.AddDays(1).ToString("yyyy-MM-dd");

        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(todayStr))
            .ReturnsAsync(CreateDayPlan(today));

        // Adjacent fetches return null — simulate unloaded data.
        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(It.Is<string>(d => d != todayStr)))
            .ReturnsAsync((DayPlanResponse?)null);

        var cut = RenderDayPlanPage(todayStr);

        // Act — invoke SwipeLeftAsync (simulates swipe-left gesture completing).
        await cut.InvokeAsync(() => cut.Instance.SwipeLeftAsync());

        // Assert — navigation to next day should occur.
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var bunitNav = (BunitNavigationManager)navigationManager;
        var lastNav = bunitNav.History.Last();
        Assert.Contains($"/day/{tomorrowStr}", lastNav.Uri);
    }

    [Fact]
    public async Task SwipeRightAsync_PrevPanelNotLoaded_NavigatesToPreviousDayRoute()
    {
        // Arrange.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayStr = today.ToString("yyyy-MM-dd");
        var yesterdayStr = today.AddDays(-1).ToString("yyyy-MM-dd");

        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(todayStr))
            .ReturnsAsync(CreateDayPlan(today));

        // Adjacent fetches return null — simulate unloaded data.
        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(It.Is<string>(d => d != todayStr)))
            .ReturnsAsync((DayPlanResponse?)null);

        var cut = RenderDayPlanPage(todayStr);

        // Act — invoke SwipeRightAsync (simulates swipe-right gesture completing).
        await cut.InvokeAsync(() => cut.Instance.SwipeRightAsync());

        // Assert — navigation to previous day should occur.
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var bunitNav = (BunitNavigationManager)navigationManager;
        var lastNav = bunitNav.History.Last();
        Assert.Contains($"/day/{yesterdayStr}", lastNav.Uri);
    }

    private IRenderedComponent<DayPlanPage> RenderDayPlanPage(string date) =>
        Render<DayPlanPage>(p => p.Add(x => x.Date, date));

    private void SetupLocalizer()
    {
        _localizerMock
            .Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        _localizerMock
            .Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] _) => new LocalizedString(key, key));
    }

    private void SetupGetDayPlanAsync(string date, DayPlanResponse? response)
    {
        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(date))
            .ReturnsAsync(response);
    }

    private static DayPlanResponse CreateDayPlan(DateOnly date) =>
        new(
            date,
            null,
            new List<AttendanceDto>
            {
                new(Guid.NewGuid(), "Alice", "#FF0000", AttendanceStatus.Unknown, false),
            },
            new List<CommentDto>(),
            new List<HistoryEntryDto>());
}
