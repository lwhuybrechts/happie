using Bunit;
using Happie.Shared.Contracts;
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

public class CalendarPagePrefetchTests : BunitContext
{
    private readonly Mock<ICachedApiClient> _cachedApiMock = new();
    private readonly Mock<IConnectivityService> _connectivityServiceMock = new();
    private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();

    private readonly List<DateOnly> _getCalendarCallOrder = new();

    public CalendarPagePrefetchTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        SetupLocalizer();
        _connectivityServiceMock.Setup(x => x.IsOnline).Returns(true);

        Services.AddSingleton(_cachedApiMock.Object);
        Services.AddSingleton(_connectivityServiceMock.Object);
        Services.AddSingleton(_localizerMock.Object);
        Services.AddSingleton(serviceProvider =>
            new LocaleService(serviceProvider.GetRequiredService<IJSRuntime>()));
        Services.AddSingleton(new LoadingIndicatorState(new FakeDelayService()));
    }

    [Fact]
    public void LoadCalendarDataAsync_CacheHit_PrefetchesNextAndPrevMonth()
    {
        // Arrange.
        var viewedMonth = new DateOnly(2025, 6, 1);
        var nextMonth = new DateOnly(2025, 7, 1);
        var prevMonth = new DateOnly(2025, 5, 1);

        SetupGetCalendarAsync(viewedMonth, CreateCalendarResponse(viewedMonth));
        SetupGetCalendarAsync(nextMonth, CreateCalendarResponse(nextMonth));
        SetupGetCalendarAsync(prevMonth, CreateCalendarResponse(prevMonth));

        // Act.
        var cut = RenderCalendarPage("2025-06-15");

        // Assert — wait for prefetch calls to complete (next + prev).
        cut.WaitForState(() => _getCalendarCallOrder.Count >= 3, TimeSpan.FromSeconds(2));

        Assert.Equal(viewedMonth, _getCalendarCallOrder[0]);
        Assert.Contains(nextMonth, _getCalendarCallOrder);
        Assert.Contains(prevMonth, _getCalendarCallOrder);
    }

    [Fact]
    public void LoadCalendarDataAsync_Offline_NoPrefetchCallsMade()
    {
        // Arrange.
        var viewedMonth = new DateOnly(2025, 6, 1);
        _connectivityServiceMock.Setup(x => x.IsOnline).Returns(false);

        _cachedApiMock
            .Setup(x => x.GetCalendarAsync(viewedMonth))
            .ReturnsAsync(new CalendarFetchResult(CreateCalendarResponse(viewedMonth), false, false, null));

        // Act.
        var cut = RenderCalendarPage("2025-06-15");

        // Assert — only the current month should be fetched, no prefetches.
        // Give a small window for any async prefetch that might fire.
        Thread.Sleep(100);

        _cachedApiMock.Verify(x => x.GetCalendarAsync(viewedMonth), Times.Once);
        _cachedApiMock.Verify(x => x.GetCalendarAsync(new DateOnly(2025, 7, 1)), Times.Never);
        _cachedApiMock.Verify(x => x.GetCalendarAsync(new DateOnly(2025, 5, 1)), Times.Never);
    }

    [Fact]
    public void OnParametersSetAsync_MonthChanges_CancelsPreviousPrefetch()
    {
        // Arrange.
        var juneMonth = new DateOnly(2025, 6, 1);
        var julyMonth = new DateOnly(2025, 7, 1);

        // Keep the June prefetch hanging via TaskCompletionSource.
        var junePrefetchTcs = new TaskCompletionSource<CalendarFetchResult>();

        _cachedApiMock
            .Setup(x => x.GetCalendarAsync(juneMonth))
            .ReturnsAsync(new CalendarFetchResult(CreateCalendarResponse(juneMonth), false, false, null));

        // The next month prefetch for June (July) will hang.
        _cachedApiMock
            .Setup(x => x.GetCalendarAsync(julyMonth))
            .Returns(junePrefetchTcs.Task);

        _cachedApiMock
            .Setup(x => x.GetCalendarAsync(new DateOnly(2025, 5, 1)))
            .ReturnsAsync(new CalendarFetchResult(CreateCalendarResponse(new DateOnly(2025, 5, 1)), false, false, null));

        var cut = RenderCalendarPage("2025-06-15");

        // Act — navigate to August (triggers new prefetch and cancels old).
        var augustMonth = new DateOnly(2025, 8, 1);
        var septMonth = new DateOnly(2025, 9, 1);
        var julyMonthForAugPrev = new DateOnly(2025, 7, 1);

        _cachedApiMock
            .Setup(x => x.GetCalendarAsync(augustMonth))
            .ReturnsAsync(new CalendarFetchResult(CreateCalendarResponse(augustMonth), false, false, null));
        _cachedApiMock
            .Setup(x => x.GetCalendarAsync(septMonth))
            .ReturnsAsync(new CalendarFetchResult(CreateCalendarResponse(septMonth), false, false, null));

        // Reset call tracking.
        _getCalendarCallOrder.Clear();

        // Re-setup to track calls during the new navigation.
        SetupGetCalendarAsyncTracked(augustMonth, CreateCalendarResponse(augustMonth));
        SetupGetCalendarAsyncTracked(septMonth, CreateCalendarResponse(septMonth));
        SetupGetCalendarAsyncTracked(julyMonthForAugPrev, CreateCalendarResponse(julyMonthForAugPrev));

        cut.Render(p => p.Add(x => x.Date, "2025-08-15"));

        // Wait for August's prefetches to complete.
        cut.WaitForState(() => _getCalendarCallOrder.Count >= 3, TimeSpan.FromSeconds(2));

        // Now complete the old hanging prefetch — it should not cause issues.
        junePrefetchTcs.SetResult(new CalendarFetchResult(CreateCalendarResponse(julyMonth), false, false, null));

        // Assert — August navigation successfully completed with its own prefetches.
        Assert.Equal(augustMonth, _getCalendarCallOrder[0]);
    }

    [Fact]
    public void PrefetchAdjacentMonths_FetchesNextMonthBeforePreviousMonth()
    {
        // Arrange.
        var viewedMonth = new DateOnly(2025, 6, 1);
        var nextMonth = new DateOnly(2025, 7, 1);
        var prevMonth = new DateOnly(2025, 5, 1);

        _cachedApiMock
            .Setup(x => x.GetCalendarAsync(It.IsAny<DateOnly>()))
            .Returns((DateOnly month) =>
            {
                _getCalendarCallOrder.Add(month);
                return Task.FromResult(new CalendarFetchResult(CreateCalendarResponse(month), false, false, null));
            });

        // Act.
        var cut = RenderCalendarPage("2025-06-15");

        // Wait for all three calls to complete.
        cut.WaitForState(() => _getCalendarCallOrder.Count >= 3, TimeSpan.FromSeconds(2));

        // Assert — next month (July) is fetched before previous month (May).
        var nextIndex = _getCalendarCallOrder.IndexOf(nextMonth);
        var prevIndex = _getCalendarCallOrder.IndexOf(prevMonth);

        Assert.True(nextIndex < prevIndex, $"Next month (index {nextIndex}) should be fetched before previous month (index {prevIndex})");
    }

    [Fact]
    public void PrefetchAdjacentMonths_NextMonthFails_StillPrefetchesPreviousMonth()
    {
        // Arrange.
        var viewedMonth = new DateOnly(2025, 6, 1);
        var nextMonth = new DateOnly(2025, 7, 1);
        var prevMonth = new DateOnly(2025, 5, 1);

        _cachedApiMock
            .Setup(x => x.GetCalendarAsync(viewedMonth))
            .ReturnsAsync(new CalendarFetchResult(CreateCalendarResponse(viewedMonth), false, false, null));

        // Next month prefetch throws.
        _cachedApiMock
            .Setup(x => x.GetCalendarAsync(nextMonth))
            .ThrowsAsync(new HttpRequestException("Network error"));

        _cachedApiMock
            .Setup(x => x.GetCalendarAsync(prevMonth))
            .ReturnsAsync(new CalendarFetchResult(CreateCalendarResponse(prevMonth), false, false, null));

        // Act.
        var cut = RenderCalendarPage("2025-06-15");

        // Assert — previous month is still fetched even though next month failed.
        cut.WaitForState(() =>
        {
            try
            {
                _cachedApiMock.Verify(x => x.GetCalendarAsync(prevMonth), Times.Once);
                return true;
            }
            catch
            {
                return false;
            }
        }, TimeSpan.FromSeconds(2));

        _cachedApiMock.Verify(x => x.GetCalendarAsync(prevMonth), Times.Once);
    }

    [Fact]
    public void PrefetchAdjacentMonths_Failure_DoesNotShowErrorUI()
    {
        // Arrange.
        var viewedMonth = new DateOnly(2025, 6, 1);
        var nextMonth = new DateOnly(2025, 7, 1);
        var prevMonth = new DateOnly(2025, 5, 1);

        _cachedApiMock
            .Setup(x => x.GetCalendarAsync(viewedMonth))
            .ReturnsAsync(new CalendarFetchResult(CreateCalendarResponse(viewedMonth), false, false, null));

        // Both prefetches throw.
        _cachedApiMock
            .Setup(x => x.GetCalendarAsync(nextMonth))
            .ThrowsAsync(new HttpRequestException("Network error"));
        _cachedApiMock
            .Setup(x => x.GetCalendarAsync(prevMonth))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act.
        var cut = RenderCalendarPage("2025-06-15");

        // Wait for prefetches to fire and fail.
        Thread.Sleep(200);

        // Assert — no error UI shown (role="alert" would contain error text).
        var errorElements = cut.FindAll("[role='alert']");
        Assert.Empty(errorElements);
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

    private void SetupGetCalendarAsync(DateOnly month, CalendarResponse response)
    {
        _cachedApiMock
            .Setup(x => x.GetCalendarAsync(month))
            .Returns((DateOnly m) =>
            {
                _getCalendarCallOrder.Add(m);
                return Task.FromResult(new CalendarFetchResult(response, false, false, null));
            });
    }

    private void SetupGetCalendarAsyncTracked(DateOnly month, CalendarResponse response)
    {
        _cachedApiMock
            .Setup(x => x.GetCalendarAsync(month))
            .Returns((DateOnly m) =>
            {
                _getCalendarCallOrder.Add(m);
                return Task.FromResult(new CalendarFetchResult(response, false, false, null));
            });
    }

    private static CalendarResponse CreateCalendarResponse(DateOnly month) =>
        new(new List<CalendarDayDto>
        {
            new(month, new List<string> { "#FF0000" }),
        });
}
