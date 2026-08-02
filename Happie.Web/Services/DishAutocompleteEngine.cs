using Happie.Shared.Contracts;
using Happie.Shared.Domain;

namespace Happie.Web.Services;

/// <summary>Client-side autocomplete matching engine for the dish input field.</summary>
public static class DishAutocompleteEngine
{
    /// <summary>
    /// Extracts the active segment from the full input text.
    /// The active segment is the text after the last delimiter, or the entire text if no delimiter exists.
    /// </summary>
    public static string ExtractActiveSegment(string inputText)
    {
        var lastDelimiterIndex = inputText.LastIndexOf(DishConstants.Delimiter, StringComparison.Ordinal);
        if (lastDelimiterIndex < 0)
            return inputText;

        return inputText[(lastDelimiterIndex + DishConstants.Delimiter.Length)..];
    }

    /// <summary>
    /// Finds the best autocomplete suggestion for the given active segment.
    /// Returns the untyped remainder of the matched dish name, or null if no match.
    /// Excludes dishes that already appear in preceding segments of the full input.
    /// </summary>
    public static string? GetSuggestion(string activeSegment, IReadOnlyList<SavedDishDto>? savedDishes, string? fullInputText = null)
    {
        if (string.IsNullOrEmpty(activeSegment))
            return null;

        if (savedDishes is null || savedDishes.Count == 0)
            return null;

        var usedDishNames = GetUsedDishNames(fullInputText, activeSegment);

        var firstMatch = savedDishes
            .Where(x => !string.IsNullOrEmpty(x.Description))
            .Where(x => x.Description.StartsWith(activeSegment, StringComparison.OrdinalIgnoreCase))
            .Where(x => !usedDishNames.Contains(x.Description))
            .OrderBy(x => x.Description, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Description)
            .FirstOrDefault();

        if (firstMatch is null)
            return null;

        if (firstMatch.Length == activeSegment.Length)
            return null;

        return firstMatch[activeSegment.Length..];
    }

    /// <summary>
    /// Collects dish names from preceding segments in the input text (before the active segment).
    /// Used to exclude already-selected dishes from autocomplete suggestions.
    /// </summary>
    private static HashSet<string> GetUsedDishNames(string? fullInputText, string activeSegment)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(fullInputText))
            return result;

        var lastDelimiterIndex = fullInputText.LastIndexOf(DishConstants.Delimiter, StringComparison.Ordinal);
        if (lastDelimiterIndex < 0)
            return result;

        var precedingText = fullInputText[..lastDelimiterIndex];
        var segments = precedingText.Split(DishConstants.Delimiter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var segment in segments)
            result.Add(segment);

        return result;
    }

    /// <summary>
    /// Builds the accepted text by replacing the active segment with the full matched dish name.
    /// Preserves all preceding text and delimiters.
    /// </summary>
    public static string AcceptSuggestion(string inputText, string matchedDishName)
    {
        var lastDelimiterIndex = inputText.LastIndexOf(DishConstants.Delimiter, StringComparison.Ordinal);
        if (lastDelimiterIndex < 0)
            return matchedDishName;

        var precedingText = inputText[..(lastDelimiterIndex + DishConstants.Delimiter.Length)];
        return precedingText + matchedDishName;
    }
}
