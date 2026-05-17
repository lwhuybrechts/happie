using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

public class PageNavigationServiceTests
{
    private static readonly DateOnly Today = new(2026, 5, 17);

    // --- GetDayPlanTarget ---

    [Fact]
    public void GetDayPlanTarget_OnDayPlanPage_AlwaysNavigatesToToday()
    {
        // Arrange.
        var relativePath = "day/2026-05-19";

        // Act.
        var result = PageNavigationService.GetDayPlanTarget(relativePath, Today);

        // Assert.
        Assert.Equal("/day/2026-05-17", result);
    }

    [Fact]
    public void GetDayPlanTarget_OnCalendarWithSelectedDate_NavigatesToThatDate()
    {
        // Arrange.
        var relativePath = "calendar/2026-05-19";

        // Act.
        var result = PageNavigationService.GetDayPlanTarget(relativePath, Today);

        // Assert.
        Assert.Equal("/day/2026-05-19", result);
    }

    [Fact]
    public void GetDayPlanTarget_OnCalendarWithoutDate_NavigatesToToday()
    {
        // Arrange.
        var relativePath = "calendar";

        // Act.
        var result = PageNavigationService.GetDayPlanTarget(relativePath, Today);

        // Assert.
        Assert.Equal("/day/2026-05-17", result);
    }

    [Fact]
    public void GetDayPlanTarget_OnHousematesPage_NavigatesToToday()
    {
        // Arrange.
        var relativePath = "housemates";

        // Act.
        var result = PageNavigationService.GetDayPlanTarget(relativePath, Today);

        // Assert.
        Assert.Equal("/day/2026-05-17", result);
    }

    [Fact]
    public void GetDayPlanTarget_OnDayPlanPageViewingToday_StaysOnToday()
    {
        // Arrange.
        var relativePath = "day/2026-05-17";

        // Act.
        var result = PageNavigationService.GetDayPlanTarget(relativePath, Today);

        // Assert.
        Assert.Equal("/day/2026-05-17", result);
    }

    // --- GetCalendarTarget ---

    [Fact]
    public void GetCalendarTarget_OnDayPlanWithSpecificDate_NavigatesToCalendarWithThatDate()
    {
        // Arrange.
        var relativePath = "day/2026-05-19";

        // Act.
        var result = PageNavigationService.GetCalendarTarget(relativePath, Today);

        // Assert.
        Assert.Equal("/calendar/2026-05-19", result);
    }

    [Fact]
    public void GetCalendarTarget_OnDayPlanViewingToday_NavigatesToCalendarWithToday()
    {
        // Arrange.
        var relativePath = "day/2026-05-17";

        // Act.
        var result = PageNavigationService.GetCalendarTarget(relativePath, Today);

        // Assert.
        Assert.Equal("/calendar/2026-05-17", result);
    }

    [Fact]
    public void GetCalendarTarget_AlreadyOnCalendarWithDate_ResetsToToday()
    {
        // Arrange.
        var relativePath = "calendar/2026-05-19";

        // Act.
        var result = PageNavigationService.GetCalendarTarget(relativePath, Today);

        // Assert.
        Assert.Equal("/calendar/2026-05-17", result);
    }

    [Fact]
    public void GetCalendarTarget_AlreadyOnCalendarWithoutDate_ResetsToToday()
    {
        // Arrange.
        var relativePath = "calendar";

        // Act.
        var result = PageNavigationService.GetCalendarTarget(relativePath, Today);

        // Assert.
        Assert.Equal("/calendar/2026-05-17", result);
    }

    [Fact]
    public void GetCalendarTarget_OnHousematesPage_NavigatesToCalendarWithToday()
    {
        // Arrange.
        var relativePath = "housemates";

        // Act.
        var result = PageNavigationService.GetCalendarTarget(relativePath, Today);

        // Assert.
        Assert.Equal("/calendar/2026-05-17", result);
    }

    // --- ExtractDateFromUrl ---

    [Fact]
    public void ExtractDateFromUrl_DayPlanUrl_ReturnsDate()
    {
        // Arrange.
        var relativePath = "day/2026-05-19";

        // Act.
        var result = PageNavigationService.ExtractDateFromUrl(relativePath, Today);

        // Assert.
        Assert.Equal("2026-05-19", result);
    }

    [Fact]
    public void ExtractDateFromUrl_CalendarUrl_ReturnsDate()
    {
        // Arrange.
        var relativePath = "calendar/2026-05-19";

        // Act.
        var result = PageNavigationService.ExtractDateFromUrl(relativePath, Today);

        // Assert.
        Assert.Equal("2026-05-19", result);
    }

    [Fact]
    public void ExtractDateFromUrl_CalendarWithoutDate_ReturnsToday()
    {
        // Arrange.
        var relativePath = "calendar";

        // Act.
        var result = PageNavigationService.ExtractDateFromUrl(relativePath, Today);

        // Assert.
        Assert.Equal("2026-05-17", result);
    }

    [Fact]
    public void ExtractDateFromUrl_HousematesPage_ReturnsToday()
    {
        // Arrange.
        var relativePath = "housemates";

        // Act.
        var result = PageNavigationService.ExtractDateFromUrl(relativePath, Today);

        // Assert.
        Assert.Equal("2026-05-17", result);
    }

    [Fact]
    public void ExtractDateFromUrl_InvalidDateInDayUrl_ReturnsToday()
    {
        // Arrange.
        var relativePath = "day/not-a-date";

        // Act.
        var result = PageNavigationService.ExtractDateFromUrl(relativePath, Today);

        // Assert.
        Assert.Equal("2026-05-17", result);
    }

    [Fact]
    public void ExtractDateFromUrl_InvalidDateInCalendarUrl_ReturnsToday()
    {
        // Arrange.
        var relativePath = "calendar/not-a-date";

        // Act.
        var result = PageNavigationService.ExtractDateFromUrl(relativePath, Today);

        // Assert.
        Assert.Equal("2026-05-17", result);
    }
}
