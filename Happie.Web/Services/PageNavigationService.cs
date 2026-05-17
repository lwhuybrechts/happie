namespace Happie.Web.Services;

/// <summary>
/// Pure logic for determining navigation targets based on the current URL context.
/// All methods are static and take the current relative path and today's date as parameters,
/// making them easy to unit test without NavigationManager.
/// </summary>
public static class PageNavigationService
{
    /// <summary>
    /// Determines the target URL when the "On the menu" / day plan nav item is clicked.
    /// If already on the day plan page, resets to today.
    /// Otherwise, navigates to the contextual date (from calendar or today as fallback).
    /// </summary>
    public static string GetDayPlanTarget(string relativePath, DateOnly today)
    {
        var todayString = today.ToString("yyyy-MM-dd");

        // If already on the day plan page, always go to today.
        if (relativePath.StartsWith("day/"))
            return $"/day/{todayString}";

        // From other pages, navigate to the contextual date.
        var date = ExtractDateFromUrl(relativePath, today);
        return $"/day/{date}";
    }

    /// <summary>
    /// Determines the target URL when the "Calendar" nav item is clicked.
    /// If already on the calendar page, resets to today.
    /// Otherwise, passes the currently viewed day so it shows as selected.
    /// </summary>
    public static string GetCalendarTarget(string relativePath, DateOnly today)
    {
        var todayString = today.ToString("yyyy-MM-dd");

        // If already on the calendar page, reset to today.
        if (relativePath.StartsWith("calendar"))
            return $"/calendar/{todayString}";

        // Pass the currently viewed day to the calendar so it shows as selected.
        var date = ExtractDateFromUrl(relativePath, today);
        return $"/calendar/{date}";
    }

    /// <summary>
    /// Extracts a date string from the current URL path.
    /// Supports "/day/yyyy-MM-dd" and "/calendar/yyyy-MM-dd" patterns.
    /// Falls back to today if no valid date is found.
    /// </summary>
    public static string ExtractDateFromUrl(string relativePath, DateOnly today)
    {
        // Extract date from "day/yyyy-MM-dd".
        if (relativePath.StartsWith("day/"))
        {
            var dateSegment = relativePath["day/".Length..];
            if (DateOnly.TryParseExact(dateSegment, "yyyy-MM-dd", out _))
                return dateSegment;
        }

        // Extract date from "calendar/yyyy-MM-dd".
        if (relativePath.StartsWith("calendar/"))
        {
            var dateSegment = relativePath["calendar/".Length..];
            if (DateOnly.TryParseExact(dateSegment, "yyyy-MM-dd", out _))
                return dateSegment;
        }

        return today.ToString("yyyy-MM-dd");
    }
}
