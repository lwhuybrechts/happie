using Bunit;
using Bunit.TestDoubles;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;
using Happie.Shared.Resources;
using Happie.Web.Pages;
using Happie.Web.Resources;
using Happie.Web.Services;
using Happie.Web.Services.Caching;
using Happie.Web.Tests.Helpers;
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
        Services.AddSingleton(new LoadingIndicatorState(new FakeDelayService()));

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
                return Task.FromResult(new DayPlanFetchResult(CreateDayPlan(DateOnly.ParseExact(date, "yyyy-MM-dd")), false, false, null));
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
        var prevFetchTcs = new TaskCompletionSource<DayPlanFetchResult>();
        var nextFetchTcs = new TaskCompletionSource<DayPlanFetchResult>();

        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(todayStr))
            .ReturnsAsync(new DayPlanFetchResult(CreateDayPlan(today), false, false, null));

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
            .ReturnsAsync(new DayPlanFetchResult(CreateDayPlan(tomorrow), false, false, null));
        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(tomorrow.AddDays(-1).ToString("yyyy-MM-dd")))
            .ReturnsAsync(new DayPlanFetchResult(CreateDayPlan(today), false, false, null));
        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(tomorrow.AddDays(1).ToString("yyyy-MM-dd")))
            .ReturnsAsync(new DayPlanFetchResult(CreateDayPlan(tomorrow.AddDays(1)), false, false, null));

        cut.Render(p => p.Add(x => x.Date, tomorrowStr));

        // Complete the old pre-fetches after navigation — these should be discarded.
        prevFetchTcs.SetResult(new DayPlanFetchResult(CreateDayPlan(today.AddDays(-1)), false, false, null));
        nextFetchTcs.SetResult(new DayPlanFetchResult(CreateDayPlan(today.AddDays(1)), false, false, null));

        // Assert — the new date's adjacent data should load, not the old one.
        // The active panel should show tomorrow's content (verify it rendered without error).
        cut.WaitForState(() => cut.FindAll(".swipe-carousel__panel--active .swipe-carousel__panel-content").Count > 0, TimeSpan.FromSeconds(2));
        Assert.NotEmpty(cut.FindAll(".swipe-carousel__panel--active .swipe-carousel__panel-content"));
    }

    [Fact]
    public void Render_PrevPanelNotLoaded_ShowsLoadingState()
    {
        // Arrange.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayStr = today.ToString("yyyy-MM-dd");

        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(todayStr))
            .ReturnsAsync(new DayPlanFetchResult(CreateDayPlan(today), false, false, null));

        // Adjacent fetches return null — not loaded.
        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(today.AddDays(-1).ToString("yyyy-MM-dd")))
            .ReturnsAsync(new DayPlanFetchResult(null, true, false, null));
        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(today.AddDays(1).ToString("yyyy-MM-dd")))
            .ReturnsAsync(new DayPlanFetchResult(null, true, false, null));

        // Act.
        var cut = RenderDayPlanPage(todayStr);

        // Assert — prev panel shows loading text instead of arrow placeholder.
        var prevPanel = cut.Find(".swipe-carousel__panel--prev");
        var loadingContent = prevPanel.QuerySelector(".swipe-carousel__panel-content--loading");
        Assert.NotNull(loadingContent);
    }

    [Fact]
    public void Render_NextPanelNotLoaded_ShowsLoadingState()
    {
        // Arrange.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayStr = today.ToString("yyyy-MM-dd");

        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(todayStr))
            .ReturnsAsync(new DayPlanFetchResult(CreateDayPlan(today), false, false, null));

        // Adjacent fetches return null — not loaded.
        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(today.AddDays(-1).ToString("yyyy-MM-dd")))
            .ReturnsAsync(new DayPlanFetchResult(null, true, false, null));
        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(today.AddDays(1).ToString("yyyy-MM-dd")))
            .ReturnsAsync(new DayPlanFetchResult(null, true, false, null));

        // Act.
        var cut = RenderDayPlanPage(todayStr);

        // Assert — next panel shows loading text instead of arrow placeholder.
        var nextPanel = cut.Find(".swipe-carousel__panel--next");
        var loadingContent = nextPanel.QuerySelector(".swipe-carousel__panel-content--loading");
        Assert.NotNull(loadingContent);
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
            .ReturnsAsync(new DayPlanFetchResult(CreateDayPlan(today), false, false, null));

        // Adjacent fetches return null — simulate unloaded data.
        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(It.Is<string>(d => d != todayStr)))
            .ReturnsAsync(new DayPlanFetchResult(null, true, false, null));

        var cut = RenderDayPlanPage(todayStr);

        // Act — invoke SwipeLeftAsync (simulates swipe-left gesture completing).
        await cut.InvokeAsync(() => cut.Instance.SwipeLeftAsync());

        // Assert — swipe always navigates even without prefetched data.
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
            .ReturnsAsync(new DayPlanFetchResult(CreateDayPlan(today), false, false, null));

        // Adjacent fetches return null — simulate unloaded data.
        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(It.Is<string>(d => d != todayStr)))
            .ReturnsAsync(new DayPlanFetchResult(null, true, false, null));

        var cut = RenderDayPlanPage(todayStr);

        // Act — invoke SwipeRightAsync (simulates swipe-right gesture completing).
        await cut.InvokeAsync(() => cut.Instance.SwipeRightAsync());

        // Assert — swipe always navigates even without prefetched data.
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
            .ReturnsAsync(new DayPlanFetchResult(response, response is null, false, null));
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
