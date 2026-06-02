using System.Globalization;
using System.Resources;
using System.Text.Json;
using System.Text.RegularExpressions;
using Happie.Shared.Domain;

namespace Happie.Shared.Resources;

/// <summary>Resolves translation keys to localized strings using shared .resx resource files.</summary>
public class SharedStringResolver
{
    private static readonly ResourceManager ResourceManager =
        new("Happie.Shared.Resources.SharedStrings", typeof(SharedStringResolver).Assembly);

    private static readonly Regex PlaceholderRegex = new(@"\{(\w+)\}", RegexOptions.Compiled);

    /// <summary>
    /// Resolves a translation key with parameters (JSON string) into a localized string for the given locale.
    /// </summary>
    public string Resolve(string translationKey, string? parameters, Locale locale)
    {
        if (string.IsNullOrEmpty(parameters))
            return ResolveWithDictionary(translationKey, null, locale);

        Dictionary<string, string>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(parameters);
        }
        catch (JsonException)
        {
            // Malformed JSON — return raw key as fallback.
            return translationKey;
        }

        return ResolveWithDictionary(translationKey, parsed, locale);
    }

    /// <summary>
    /// Resolves a translation key with a pre-parsed parameters dictionary.
    /// </summary>
    public string Resolve(string translationKey, Dictionary<string, string>? parameters, Locale locale)
    {
        return ResolveWithDictionary(translationKey, parameters, locale);
    }

    private string ResolveWithDictionary(string translationKey, Dictionary<string, string>? parameters, Locale locale)
    {
        var culture = GetCultureInfo(locale);
        var template = ResourceManager.GetString(translationKey, culture);

        // If key not found, return the raw translation key as fallback.
        if (template is null)
            return translationKey;

        // If parameters are null or empty, return template without substitution.
        if (parameters is null || parameters.Count == 0)
            return template;

        // Substitute each {placeholder} with the corresponding parameter value.
        var result = PlaceholderRegex.Replace(template, match =>
        {
            var placeholder = match.Groups[1].Value;

            if (!parameters.TryGetValue(placeholder, out var value))
                return match.Value;

            // Special-case: resolve status enum value to localized display name.
            if (placeholder == "status")
                return ResolveStatus(value, culture);

            // Special-case: resolve enabled true/false to localized display name.
            if (placeholder == "enabled")
                return ResolveEnabled(value, culture);

            return value;
        });

        return result;
    }

    private static string ResolveStatus(string rawValue, CultureInfo culture)
    {
        var displayName = ResourceManager.GetString($"status_{rawValue}", culture);

        // If unknown status value, pass through unchanged.
        return displayName ?? rawValue;
    }

    private static string ResolveEnabled(string rawValue, CultureInfo culture)
    {
        var displayName = ResourceManager.GetString($"enabled_{rawValue}", culture);

        // If unknown enabled value, pass through unchanged.
        return displayName ?? rawValue;
    }

    private static CultureInfo GetCultureInfo(Locale locale) =>
        locale switch
        {
            Locale.Nl => new CultureInfo("nl-NL"),
            Locale.En => new CultureInfo("en-US"),
            _ => throw new InvalidOperationException($"Unhandled {nameof(Locale)}: {locale}"),
        };
}
