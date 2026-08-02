using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.Handlers;

/// <summary>
/// Matches a dish description (single or multi-part separated by " &amp; ") against a collection
/// of saved dishes. Used by both <see cref="DayHandler"/> (Auto_Match on save) and
/// <see cref="SavedDishHandler"/> (retroactive conversion).
/// </summary>
internal static class SavedDishMatcher
{
    /// <summary>
    /// Tries to match a trimmed description against saved dishes. Handles both single exact match
    /// and multi-part descriptions split by " &amp; ". Returns the matched dishes in order,
    /// or null if no full match was found.
    /// </summary>
    internal static List<SavedDish>? TryMatchAll(string trimmedDescription, IReadOnlyList<SavedDish>? allSavedDishes)
    {
        if (allSavedDishes is null || allSavedDishes.Count == 0)
            return null;

        // Split into segments: a single description without " & " yields one segment.
        var segments = trimmedDescription.Contains(DishConstants.Delimiter)
            ? trimmedDescription.Split(DishConstants.Delimiter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [trimmedDescription];

        if (segments.Length == 0 || segments.Length > 10)
            return null;

        var matchedDishes = new List<SavedDish>(segments.Length);
        foreach (var segment in segments)
        {
            var segmentMatch = allSavedDishes.FirstOrDefault(x =>
                string.Equals(x.Description.Trim(), segment, StringComparison.OrdinalIgnoreCase));

            if (segmentMatch is null)
                return null;

            matchedDishes.Add(segmentMatch);
        }

        return matchedDishes;
    }
}
