namespace Happie.Web.Services;

/// <summary>Pure logic for building the calendar grid layout.</summary>
public static class CalendarGridService
{
    /// <summary>
    /// Builds the list of dates visible in the calendar grid for a given month.
    /// The grid always starts on Monday and ends on Sunday, including filler days from adjacent months.
    /// </summary>
    public static IReadOnlyList<DateOnly> GetVisibleDates(DateOnly viewedMonth)
    {
        var firstOfMonth = new DateOnly(viewedMonth.Year, viewedMonth.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(viewedMonth.Year, viewedMonth.Month);
        var lastOfMonth = new DateOnly(viewedMonth.Year, viewedMonth.Month, daysInMonth);

        // Find the Monday on or before the first of the month.
        var startDate = firstOfMonth;
        while (startDate.DayOfWeek != DayOfWeek.Monday)
            startDate = startDate.AddDays(-1);

        // Find the Sunday on or after the last of the month.
        var endDate = lastOfMonth;
        while (endDate.DayOfWeek != DayOfWeek.Sunday)
            endDate = endDate.AddDays(1);

        var dates = new List<DateOnly>();
        var current = startDate;
        while (current <= endDate)
        {
            dates.Add(current);
            current = current.AddDays(1);
        }

        return dates;
    }

    /// <summary>
    /// Returns the start and end dates for the API query that covers the full visible grid.
    /// </summary>
    public static (DateOnly Start, DateOnly End) GetVisibleDateRange(DateOnly viewedMonth)
    {
        var dates = GetVisibleDates(viewedMonth);
        return (dates[0], dates[^1]);
    }
}
