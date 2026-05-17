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

        // 7+ days away (past or future) or 2–6 days in the past: no title, bold date only.
        if (Math.Abs(offset) >= 7 || offset <= -2)
            return new DateLabel(null, formattedDate, TitleIsBold: false, DateIsBold: true);

        var title = offset switch
        {
            0 => todayLabel,
            -1 => yesterdayLabel,
            1 => tomorrowLabel,
            // +2 to +6: show day name.
            _ => viewedDate.ToString("dddd", culture),
        };

        return new DateLabel(title, formattedDate, TitleIsBold: true, DateIsBold: false);
    }
}
