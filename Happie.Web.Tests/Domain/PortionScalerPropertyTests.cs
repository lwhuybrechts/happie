using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Domain;
using Happie.Web.Domain;

namespace Happie.Web.Tests.Domain;

/// <summary>Property-based tests for <see cref="PortionScaler"/>.</summary>
public class PortionScalerPropertyTests
{
    // Feature: dish-recipes, Property 3: Portion Scaling Calculation
    /// <summary>
    /// For any ingredient with a positive base amount, a base serving count between 1 and 25,
    /// and an adjusted serving count between 1 and 25, the scaled amount SHALL equal
    /// baseAmount * (adjustedServings / baseServings).
    /// **Validates: Requirements 5.4, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Scale_PositiveAmountWithValidServings_ReturnsCorrectScaledAmount()
    {
        return Prop.ForAll(
            PositiveAmountArb(),
            ServingsArb(),
            ServingsArb(),
            (baseAmount, baseServings, adjustedServings) =>
            {
                // Arrange.
                var expected = baseAmount * ((double)adjustedServings / baseServings);

                // Act.
                var result = PortionScaler.Scale(baseAmount, baseServings, adjustedServings);

                // Assert.
                var withinTolerance = Math.Abs(result - expected) < 1e-10;
                return withinTolerance
                    .Label($"Expected {expected} but got {result} for baseAmount={baseAmount}, baseServings={baseServings}, adjustedServings={adjustedServings}");
            });
    }

    // Feature: dish-recipes, Property 4: Amount Formatting by Unit Type
    /// <summary>
    /// For any positive amount and any valid unit of measurement, the formatted output SHALL use
    /// common fractions (1/4, 1/3, 1/2, 2/3, 3/4) for count-based units (piece, stalk, clove, can,
    /// slice, bunch, handful) and SHALL use exactly 2 decimal places for weight/volume units
    /// (g, kg, ml, l, tbsp, tsp, pinch, cup).
    /// **Validates: Requirements 5.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FormatAmount_CountBasedUnit_ReturnsFractionFormat()
    {
        return Prop.ForAll(
            PositiveAmountArb(),
            CountBasedUnitArb(),
            (amount, unit) =>
            {
                // Act.
                var result = PortionScaler.FormatAmount(amount, unit);

                // Assert.
                var isValidFractionFormat = IsValidFractionOutput(result);
                return isValidFractionFormat
                    .Label($"Expected valid fraction format but got '{result}' for amount={amount}, unit={unit}");
            });
    }

    // Feature: dish-recipes, Property 4: Amount Formatting by Unit Type
    /// <summary>
    /// For any positive amount and any weight/volume unit of measurement, the formatted output
    /// SHALL use exactly 2 decimal places.
    /// **Validates: Requirements 5.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FormatAmount_WeightVolumeUnit_ReturnsTwoDecimalPlaces()
    {
        return Prop.ForAll(
            PositiveAmountArb(),
            WeightVolumeUnitArb(),
            (amount, unit) =>
            {
                // Act.
                var result = PortionScaler.FormatAmount(amount, unit);

                // Assert.
                // Accept both '.' and ',' as decimal separator depending on system culture.
                var matchesTwoDecimals = Regex.IsMatch(result, @"^\d+[.,]\d{2}$");
                return matchesTwoDecimals
                    .Label($"Expected format with exactly 2 decimal places but got '{result}' for amount={amount}, unit={unit}");
            });
    }

    private static Arbitrary<double> PositiveAmountArb()
    {
        // Generate amounts between 0.01 and 9999 (valid ingredient amounts).
        var gen = Gen.Choose(1, 999900)
            .Select(x => x / 100.0);
        return Arb.From(gen);
    }

    private static Arbitrary<int> ServingsArb()
    {
        // Generate servings between 1 and 25 (valid range).
        return Arb.From(Gen.Choose(RecipeConstants.MinServings, RecipeConstants.MaxServings));
    }

    private static Arbitrary<UnitOfMeasurement> CountBasedUnitArb()
    {
        return Arb.From(Gen.Elements(
            UnitOfMeasurement.Piece, UnitOfMeasurement.Stalk, UnitOfMeasurement.Clove,
            UnitOfMeasurement.Can, UnitOfMeasurement.Slice, UnitOfMeasurement.Bunch,
            UnitOfMeasurement.Handful));
    }

    private static Arbitrary<UnitOfMeasurement> WeightVolumeUnitArb()
    {
        return Arb.From(Gen.Elements(
            UnitOfMeasurement.G, UnitOfMeasurement.Kg, UnitOfMeasurement.Ml,
            UnitOfMeasurement.L, UnitOfMeasurement.Tbsp, UnitOfMeasurement.Tsp,
            UnitOfMeasurement.Pinch, UnitOfMeasurement.Cup));
    }

    private static readonly HashSet<string> ValidFractions = new()
    {
        "1/4", "1/3", "1/2", "2/3", "3/4"
    };

    private static bool IsValidFractionOutput(string result)
    {
        // Valid outputs: a whole number, a fraction, or a whole number followed by a fraction.
        if (string.IsNullOrEmpty(result))
            return false;

        // Pure fraction (e.g., "1/2").
        if (ValidFractions.Contains(result))
            return true;

        // Pure whole number (e.g., "3").
        if (Regex.IsMatch(result, @"^\d+$"))
            return true;

        // Whole number followed by space and fraction (e.g., "2 1/2").
        var parts = result.Split(' ');
        if (parts.Length == 2 && Regex.IsMatch(parts[0], @"^\d+$") && ValidFractions.Contains(parts[1]))
            return true;

        return false;
    }
}
