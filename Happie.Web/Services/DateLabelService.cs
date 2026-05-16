using System.Globalization;

namespace Happie.Web.Services;

/// <summary>Computes contextual date labels for the date navigation panel.</summary>
public static class DateLabelService
{
    /// <summary>Returns a contextual label for the viewed date relative to today.</summary>
    public static DateLabel GetLabel(DateOnly viewedDate, DateOnly today, CultureInfo culture)
    {
        var offset = viewedDate.DayNumber - today.DayNumber;
        var formattedDate = viewedDate.ToString("d MMM yyyy", culture);

        if (Math.Abs(offset) >= 7)
            return new DateLabel(null, formattedDate, TitleIsBold: false, DateIsBold: true);

        var title = offset switch
        {
            0 => "Today",
            -1 => "Yesterday",
            1 => "Tomorrow",
            _ => viewedDate.ToString("dddd", culture),
        };

        return new DateLabel(title, formattedDate, TitleIsBold: true, DateIsBold: false);
    }
}
