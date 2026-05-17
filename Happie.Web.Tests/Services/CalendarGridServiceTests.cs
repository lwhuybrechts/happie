using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

public class CalendarGridServiceTests
{
    [Fact]
    public void GetVisibleDates_May2026_StartsOnMonday()
    {
        // Arrange.
        var viewedMonth = new DateOnly(2026, 5, 1);

        // Act.
        var dates = CalendarGridService.GetVisibleDates(viewedMonth);

        // Assert.
        Assert.Equal(DayOfWeek.Monday, dates[0].DayOfWeek);
    }

    [Fact]
    public void GetVisibleDates_May2026_EndsOnSunday()
    {
        // Arrange.
        var viewedMonth = new DateOnly(2026, 5, 1);

        // Act.
        var dates = CalendarGridService.GetVisibleDates(viewedMonth);

        // Assert.
        Assert.Equal(DayOfWeek.Sunday, dates[^1].DayOfWeek);
    }

    [Fact]
    public void GetVisibleDates_May2026_CountIsDivisibleBySeven()
    {
        // Arrange.
        var viewedMonth = new DateOnly(2026, 5, 1);

        // Act.
        var dates = CalendarGridService.GetVisibleDates(viewedMonth);

        // Assert.
        Assert.Equal(0, dates.Count % 7);
    }

    [Fact]
    public void GetVisibleDates_May2026_IncludesFillerDaysFromApril()
    {
        // Arrange.
        // May 2026 starts on a Friday, so the grid should include Mon 27 Apr – Thu 30 Apr.
        var viewedMonth = new DateOnly(2026, 5, 1);

        // Act.
        var dates = CalendarGridService.GetVisibleDates(viewedMonth);

        // Assert.
        Assert.Equal(new DateOnly(2026, 4, 27), dates[0]);
    }

    [Fact]
    public void GetVisibleDates_May2026_IncludesFillerDaysFromJune()
    {
        // Arrange.
        // May 2026 ends on a Sunday, so no filler days needed at the end.
        var viewedMonth = new DateOnly(2026, 5, 1);

        // Act.
        var dates = CalendarGridService.GetVisibleDates(viewedMonth);

        // Assert.
        Assert.Equal(new DateOnly(2026, 5, 31), dates[^1]);
    }

    [Fact]
    public void GetVisibleDates_February2026_IncludesFillerDaysFromJanuaryAndMarch()
    {
        // Arrange.
        // Feb 2026 starts on a Sunday, so the grid starts on Mon 26 Jan.
        // Feb 2026 ends on a Saturday, so the grid ends on Sun 1 Mar.
        var viewedMonth = new DateOnly(2026, 2, 1);

        // Act.
        var dates = CalendarGridService.GetVisibleDates(viewedMonth);

        // Assert.
        Assert.Equal(new DateOnly(2026, 1, 26), dates[0]);
        Assert.Equal(new DateOnly(2026, 3, 1), dates[^1]);
    }

    [Fact]
    public void GetVisibleDates_MonthStartingOnMonday_NoLeadingFillerDays()
    {
        // Arrange.
        // June 2026 starts on a Monday.
        var viewedMonth = new DateOnly(2026, 6, 1);

        // Act.
        var dates = CalendarGridService.GetVisibleDates(viewedMonth);

        // Assert.
        Assert.Equal(new DateOnly(2026, 6, 1), dates[0]);
    }

    [Fact]
    public void GetVisibleDates_MonthEndingOnSunday_NoTrailingFillerDays()
    {
        // Arrange.
        // May 2026 ends on a Sunday (31st).
        var viewedMonth = new DateOnly(2026, 5, 1);

        // Act.
        var dates = CalendarGridService.GetVisibleDates(viewedMonth);

        // Assert.
        Assert.Equal(new DateOnly(2026, 5, 31), dates[^1]);
    }

    [Fact]
    public void GetVisibleDates_AllDatesAreConsecutive()
    {
        // Arrange.
        var viewedMonth = new DateOnly(2026, 5, 1);

        // Act.
        var dates = CalendarGridService.GetVisibleDates(viewedMonth);

        // Assert.
        for (var i = 1; i < dates.Count; i++)
            Assert.Equal(dates[i - 1].AddDays(1), dates[i]);
    }

    [Fact]
    public void GetVisibleDateRange_May2026_ReturnsFirstAndLastVisibleDates()
    {
        // Arrange.
        var viewedMonth = new DateOnly(2026, 5, 1);

        // Act.
        var (start, end) = CalendarGridService.GetVisibleDateRange(viewedMonth);

        // Assert.
        Assert.Equal(new DateOnly(2026, 4, 27), start);
        Assert.Equal(new DateOnly(2026, 5, 31), end);
    }

    [Fact]
    public void GetVisibleDateRange_February2026_CoversFullGrid()
    {
        // Arrange.
        var viewedMonth = new DateOnly(2026, 2, 1);

        // Act.
        var (start, end) = CalendarGridService.GetVisibleDateRange(viewedMonth);

        // Assert.
        Assert.Equal(new DateOnly(2026, 1, 26), start);
        Assert.Equal(new DateOnly(2026, 3, 1), end);
    }
}
