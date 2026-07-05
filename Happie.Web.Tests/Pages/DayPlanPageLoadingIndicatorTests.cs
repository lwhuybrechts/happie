using Bunit;
using Happie.Shared.Contracts;
using Happie.Shared.Resources;
using Happie.Web.Pages;
using Happie.Web.Resources;
using Happie.Web.Services;
using Happie.Web.Services.Caching;
using Happie.Web.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Moq;

namespace Happie.Web.Tests.Pages;

public class DayPlanPageLoadingIndicatorTests : BunitContext
{
    private readonly Mock<ICachedApiClient> _cachedApiMock = new();
    private readonly Mock<IConnectivityService> _connectivityServiceMock = new();
    private readonly Mock<ISyncService> _syncServiceMock = new();
    private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();
    private readonly LoadingIndicatorState _loadingIndicatorState;
    private readonly FakeDelayService _fakeDelayService;

    public DayPlanPageLoadingIndicatorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _fakeDelayService = new FakeDelayService();
        _loadingIndicatorState = new LoadingIndicatorState(_fakeDelayService);

        SetupLocalizer();
        _connectivityServiceMock.Setup(x => x.IsOnline).Returns(true);

        Services.AddSingleton(_cachedApiMock.Object);
        Services.AddSingleton(_connectivityServiceMock.Object);
        Services.AddSingleton(_syncServiceMock.Object);
        Services.AddSingleton(_localizerMock.Object);
        Services.AddSingleton(_loadingIndicatorState);
        Services.AddScoped(serviceProvider =>
            new ActiveHousemateService(serviceProvider.GetRequiredService<IJSRuntime>()));
        Services.AddSingleton(serviceProvider =>
            new LocaleService(serviceProvider.GetRequiredService<IJSRuntime>()));
        Services.AddSingleton<SharedStringResolver>();

        // HttpClient needed by NudgeModal.
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost/api/") };
        Services.AddSingleton(httpClient);
    }

    [Fact]
    public async Task LoadDayPlanAsync_CacheHitWithBackgroundRefreshTask_ActivatesLoadingIndicator()
    {
        // Arrange.
        var refreshTaskCompletionSource = new TaskCompletionSource();

        SetupGetDayPlanAsync("2025-06-15", CreateDayPlanFetchResult(
            data: CreateDayPlanResponse(),
            backgroundRefreshTask: refreshTaskCompletionSource.Task));

        // Act — render the page (LoadDayPlanAsync will suspend at the background refresh await).
        var cut = Render<DayPlanPage>(parameters => parameters.Add(x => x.Date, "2025-06-15"));

        // Assert — loading indicator is visible while the background refresh task is pending.
        Assert.True(_loadingIndicatorState.IsVisible);

        // Complete the refresh task and trigger the minimum visibility timer.
        refreshTaskCompletionSource.SetResult();
        await Task.Delay(50);
        await _fakeDelayService.TriggerTimerAsync();

        Assert.False(_loadingIndicatorState.IsVisible);
    }

    [Fact]
    public void PreFetchAdjacentDaysAsync_BackgroundRefreshTaskOnAdjacentDays_DoesNotActivateLoadingIndicator()
    {
        // Arrange.
        var adjacentPrevRefreshTcs = new TaskCompletionSource();
        var adjacentNextRefreshTcs = new TaskCompletionSource();

        // Active date returns immediately with no background refresh task.
        SetupGetDayPlanAsync("2025-06-15", CreateDayPlanFetchResult(
            data: CreateDayPlanResponse(),
            backgroundRefreshTask: null));

        // Adjacent days return with hanging background refresh tasks.
        SetupGetDayPlanAsync("2025-06-14", CreateDayPlanFetchResult(
            data: CreateDayPlanResponse(),
            backgroundRefreshTask: adjacentPrevRefreshTcs.Task));

        SetupGetDayPlanAsync("2025-06-16", CreateDayPlanFetchResult(
            data: CreateDayPlanResponse(),
            backgroundRefreshTask: adjacentNextRefreshTcs.Task));

        // Act.
        var cut = RenderDayPlanPage("2025-06-15");

        // Wait for prefetches to fire (adjacent panels should load).
        cut.WaitForState(() => cut.FindAll(".swipe-carousel__panel-content").Count >= 2, TimeSpan.FromSeconds(2));

        // Assert — loading indicator should NOT be visible for adjacent-day prefetches.
        Assert.False(_loadingIndicatorState.IsVisible);

        // Clean up hanging tasks.
        adjacentPrevRefreshTcs.SetResult();
        adjacentNextRefreshTcs.SetResult();
    }

    private IRenderedComponent<DayPlanPage> RenderDayPlanPage(string date) =>
        Render<DayPlanPage>(parameters => parameters.Add(x => x.Date, date));

    private void SetupLocalizer()
    {
        _localizerMock
            .Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        _localizerMock
            .Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] _) => new LocalizedString(key, key));
    }

    private void SetupGetDayPlanAsync(string date, DayPlanFetchResult result)
    {
        _cachedApiMock
            .Setup(x => x.GetDayPlanAsync(date))
            .ReturnsAsync(result);
    }

    private static DayPlanFetchResult CreateDayPlanFetchResult(DayPlanResponse? data, Task? backgroundRefreshTask) =>
        new(data, false, false, backgroundRefreshTask);

    private static DayPlanResponse CreateDayPlanResponse() =>
        new(
            Date: new DateOnly(2025, 6, 15),
            Dish: null,
            Attendance: new List<AttendanceDto>(),
            Comments: new List<CommentDto>(),
            History: new List<HistoryEntryDto>());
}
