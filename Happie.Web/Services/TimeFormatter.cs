using System.Globalization;

namespace Happie.Web.Services;

/// <summary>Formats timestamps for display in the day plan UI.</summary>
public static class TimeFormatter
{
    /// <summary>Formats a dish edit timestamp as a relative or absolute time string.</summary>
    public static string FormatDishTime(DateTimeOffset editedAt, DateTimeOffset now, string justNow = "just now", string minAgo = "min ago", string hoursAgo = "hours ago")
    {
        var elapsed = now - editedAt;

        if (elapsed.TotalSeconds < 60)
            return justNow;

        if (elapsed.TotalMinutes < 60)
            return $"{(int)elapsed.TotalMinutes} {minAgo}";

        if (elapsed.TotalHours < 3)
            return $"{(int)elapsed.TotalHours} {hoursAgo}";

        if (editedAt.Date == now.Date)
            return editedAt.ToString("HH:mm", CultureInfo.InvariantCulture);

        return editedAt.ToString("d MMM HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>Formats a history entry timestamp based on calendar proximity.</summary>
    public static string FormatHistoryTime(DateTimeOffset changedAt, DateTimeOffset now)
    {
        if (changedAt.Date == now.Date)
            return changedAt.ToString("HH:mm", CultureInfo.InvariantCulture);

        if (changedAt.Year == now.Year)
            return changedAt.ToString("d MMM HH:mm", CultureInfo.InvariantCulture);

        return changedAt.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture);
    }
}
