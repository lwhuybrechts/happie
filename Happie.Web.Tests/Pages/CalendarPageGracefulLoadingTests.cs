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
using Microsoft.JSInterop;
using Moq;

namespace Happie.Web.Tests.Pages;

public class CalendarPageGracefulLoadingTests : BunitContext
{
    private readonly Mock<ICachedApiClient> _cachedApiMock = new();
    private readonly Mock<IConnectivityService> _connectivityServiceMock = new();
    private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();
    private readonly FakeDelayService _fakeDelayService = new();
    private readonly LoadingIndicatorState _loadingIndicator;

    public CalendarPageGracefulLoadingTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        SetupLocalizer();
        _connectivityServiceMock.Setup(x => x.IsOnline).Returns(true);

        _loadingIndicator = new LoadingIndicatorState(_fakeDelayService);

        Services.AddSingleton(_cachedApiMock.Object);
        Services.AddSingleton(_connectivityServiceMock.Object);
        Services.AddSingleton(_localizerMock.Object);
        Services.AddSingleton(serviceProvider =>
            new LocaleService(serviceProvider.GetRequiredService<IJSRuntime>()));
        Services.AddSingleton(_loadingIndicator);
    }

    [Fact]
    public void LoadCalendarDataAsync_ColdCacheOnline_RendersCalendarGridWithEmptyDays()
    {
        // Arrange.
        var tcs = new TaskCompletionSource<CalendarFetchResult>();
        _cachedApiMock
            .Setup(x => x.GetCalendarAsync(It.IsAny<DateOnly>()))
            .Returns(tcs.Task);

        // Act.
        var cut = RenderCalendarPage();

        // Assert — CalendarGrid renders (day buttons visible) but no dots.
        var dayButtons = cut.FindAll(".calendar-grid__cell");
        Assert.NotEmpty(dayButtons);

        var dots = cut.FindAll(".calendar-grid__dot");
        Assert.Empty(dots);

        Assert.True(_loadingIndicator.IsVisible);

        // Clean up.
        tcs.SetResult(new CalendarFetchResult(null, true, true, null));
    }

    [Fact]
    public async Task LoadCalendarDataAsync_FetchCompletes_TransitionsToFullData()
    {
        // Arrange.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var viewedMonth = new DateOnly(today.Year, today.Month, 1);
        var calendarResponse = CreateCalendarResponse(viewedMonth);

        var tcs = new TaskCompletionSource<CalendarFetchResult>();
        _cachedApiMock
            .Setup(x => x.GetCalendarAsync(It.IsAny<DateOnly>()))
            .Returns(tcs.Task);

        var cut = RenderCalendarPage();

        // Verify graceful loading state (no dots).
        Assert.Empty(cut.FindAll(".calendar-grid__dot"));

        // Act — complete the fetch with data.
        tcs.SetResult(new CalendarFetchResult(calendarResponse, true, false, null));
        cut.WaitForState(() => cut.FindAll(".calendar-grid__dot").Count > 0, TimeSpan.FromSeconds(2));

        // Assert — dots are now rendered.
        var dots = cut.FindAll(".calendar-grid__dot");
        Assert.NotEmpty(dots);

        // Trigger the minimum visibility timer so the indicator hides.
        await _fakeDelayService.TriggerTimerAsync();
        Assert.False(_loadingIndicator.IsVisible);
    }

    [Fact]
    public async Task LoadCalendarDataAsync_FetchFails_TransitionsToErrorState()
    {
        // Arrange.
        var tcs = new TaskCompletionSource<CalendarFetchResult>();
        _cachedApiMock
            .Setup(x => x.GetCalendarAsync(It.IsAny<DateOnly>()))
            .Returns(tcs.Task);

        var cut = RenderCalendarPage();

        // Act — complete the fetch with an error.
        tcs.SetResult(new CalendarFetchResult(null, true, true, null));
        cut.WaitForState(() => cut.FindAll("[role=\"alert\"]").Count > 0, TimeSpan.FromSeconds(2));

        // Assert — error state with alert and retry button.
        var alert = cut.Find("[role=\"alert\"]");
        Assert.NotNull(alert);

        var buttons = cut.FindAll("button");
        Assert.Contains(buttons, x => x.TextContent.Contains("Calendar_Retry"));

        // Trigger the minimum visibility timer so the indicator hides.
        await _fakeDelayService.TriggerTimerAsync();
        Assert.False(_loadingIndicator.IsVisible);
    }

    [Fact]
    public void LoadCalendarDataAsync_GracefulLoading_DayButtonsClickable()
    {
        // Arrange.
        var tcs = new TaskCompletionSource<CalendarFetchResult>();
        _cachedApiMock
            .Setup(x => x.GetCalendarAsync(It.IsAny<DateOnly>()))
            .Returns(tcs.Task);

        var cut = RenderCalendarPage();

        // Wait for the grid to render cells (async lifecycle yields before blocking on the TCS).
        cut.WaitForState(() => cut.FindAll(".calendar-grid__cell").Count > 0, TimeSpan.FromSeconds(2));

        // Act — click a day button during graceful loading.
        var dayButtons = cut.FindAll(".calendar-grid__cell");
        Assert.NotEmpty(dayButtons);

        dayButtons.Last().Click();

        // Assert — navigation occurred to the DayPlanPage.
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var bunitNav = (BunitNavigationManager)navigationManager;
        var lastNav = bunitNav.History.Last();
        Assert.Contains("/day/", lastNav.Uri);

        // Clean up.
        tcs.SetResult(new CalendarFetchResult(null, true, true, null));
    }

    private IRenderedComponent<CalendarPage> RenderCalendarPage(string? date = null) =>
        Render<CalendarPage>(p => p.Add(x => x.Date, date));

    private void SetupLocalizer()
    {
        _localizerMock
            .Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        _localizerMock
            .Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] _) => new LocalizedString(key, key));
    }

    private static CalendarResponse CreateCalendarResponse(DateOnly month) =>
        new(new List<CalendarDayDto>
        {
            new(month.AddDays(4), new List<string> { "#FF0000", "#00FF00" }),
            new(month.AddDays(10), new List<string> { "#0000FF" }),
        });
}
