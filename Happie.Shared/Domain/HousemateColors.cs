namespace Happie.Shared.Domain;

/// <summary>Predefined color palette for housemate visual identification.</summary>
public static class HousemateColors
{
    /// <summary>
    /// Exactly 30 hex color values, balanced across the spectrum and including
    /// warm/feminine tones for easy distinction.
    /// </summary>
    public static readonly IReadOnlyList<string> Palette =
    [
        // Pinks & roses.
        "#F06292",
        "#E91E63",
        "#AD1457",
        "#F48FB1",
        // Purples & lilacs.
        "#CE93D8",
        "#9C27B0",
        "#6A1B9A",
        "#B39DDB",
        // Reds & oranges.
        "#EF5350",
        "#FF7043",
        "#FFA726",
        "#FFCA28",
        // Yellows & greens.
        "#D4E157",
        "#8BC34A",
        "#43A047",
        "#00897B",
        // Teals & blues.
        "#26C6DA",
        "#039BE5",
        "#1E88E5",
        "#3949AB",
        // Deep blues & navy.
        "#283593",
        "#0277BD",
        // Greens (earthy).
        "#558B2F",
        "#33691E",
        // Browns & neutrals.
        "#8D6E63",
        "#6D4C41",
        // Greys & slates.
        "#546E7A",
        "#78909C",
        "#757575",
        "#455A64",
    ];
}
