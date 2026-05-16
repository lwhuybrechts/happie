namespace Happie.Web.Services;

/// <summary>Represents a contextual date label with an optional title and a formatted date string.</summary>
public record DateLabel(string? Title, string FormattedDate, bool TitleIsBold, bool DateIsBold);
