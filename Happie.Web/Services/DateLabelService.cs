using System.Globalization;

namespace Happie.Web.Services;

/// <summary>Computes contextual date labels for the date navigation panel.</summary>
public static class DateLabelService
{
    /// <summary>Returns a contextual label for the viewed date relative to today.</summary>
    public static DateLabel GetLabel(DateOnly viewedDate, DateOnly today, CultureInfo culture, string todayLabel = "Today", string yesterdayLabel = "Yesterday", string tomorrowLabel = "Tomorrow")
    {
        var offset = viewedDate.DayNumber - today.DayNumber;
        var formattedDate = viewedDate.ToString("d MMM yyyy", culture);

        var title = offset switch
        {
            0 => todayLabel,
            -1 => yesterdayLabel,
            1 => tomorrowLabel,
            // All other dates: show weekday name.
            _ => viewedDate.ToString("dddd", culture),
        };

        return new DateLabel(title, formattedDate, TitleIsBold: true, DateIsBold: false);
    }
}
