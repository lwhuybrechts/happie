using Happie.Shared.Domain;

namespace Happie.Web.Domain;

/// <summary>Client-side utility for scaling ingredient amounts by portion count.</summary>
public static class PortionScaler
{
    /// <summary>Scales a base amount by the ratio of adjusted to base servings.</summary>
    public static double Scale(double baseAmount, int baseServings, int adjustedServings)
    {
        return baseAmount * ((double)adjustedServings / baseServings);
    }

    /// <summary>Formats an amount for display based on unit type.</summary>
    public static string FormatAmount(double amount, UnitOfMeasurement unit)
    {
        if (RecipeConstants.CountBasedUnits.Contains(unit))
            return FormatAsFraction(amount);

        return amount.ToString("F2");
    }

    // Format using common fractions: 1/2, 1/3, 1/4, 3/4.
    private static string FormatAsFraction(double amount)
    {
        var wholePart = (int)amount;
        var fractionalPart = amount - wholePart;

        // Match to nearest common fraction.
        var fraction = fractionalPart switch
        {
            >= 0.0 and < 0.125 => "",
            >= 0.125 and < 0.29 => "1/4",
            >= 0.29 and < 0.415 => "1/3",
            >= 0.415 and < 0.585 => "1/2",
            >= 0.585 and < 0.71 => "2/3",
            >= 0.71 and < 0.875 => "3/4",
            _ => ""
        };

        if (string.IsNullOrEmpty(fraction) && fractionalPart >= 0.875)
            wholePart++;

        if (wholePart == 0 && !string.IsNullOrEmpty(fraction))
            return fraction;

        if (string.IsNullOrEmpty(fraction))
            return wholePart.ToString();

        return $"{wholePart} {fraction}";
    }
}
