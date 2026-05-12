namespace Happie.Shared.Domain;

/// <summary>Extension methods for converting between the <see cref="Locale"/> enum and culture code strings.</summary>
public static class LocaleExtensions
{
    /// <summary>The default locale used when no preference has been set.</summary>
    public static readonly Locale Default = Locale.Nl;

    /// <summary>Returns the BCP 47 culture code for the locale (e.g. "nl" or "en").</summary>
    public static string ToCultureCode(this Locale locale) =>
        locale switch
        {
            Locale.Nl => "nl",
            Locale.En => "en",
            _ => throw new InvalidOperationException($"Unhandled {nameof(Locale)}: {locale}"),
        };

    /// <summary>Parses a BCP 47 culture code string into a <see cref="Locale"/>. Returns the default locale when the code is unrecognised or null.</summary>
    public static Locale ToLocale(this string? cultureCode) =>
        cultureCode switch
        {
            "en" => Locale.En,
            "nl" => Locale.Nl,
            _ => Default,
        };
}
