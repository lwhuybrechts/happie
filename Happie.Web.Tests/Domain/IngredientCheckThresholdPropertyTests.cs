using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Domain;
using Happie.Web.Domain;

namespace Happie.Web.Tests.Domain;

// Feature: dish-recipes, Property 5: For any non-empty list of ingredients with random checked/unchecked states, the toggle button label SHALL be "Check all" when 50% or fewer are checked, and "Uncheck all" when more than 50% are checked.

/// <summary>Property-based tests for <see cref="IngredientCheckThreshold"/>.</summary>
public class IngredientCheckThresholdPropertyTests
{
    /// <summary>
    /// For any non-empty list of ingredients with random checked/unchecked states,
    /// the toggle button label SHALL be "Check all" when 50% or fewer are checked,
    /// and "Uncheck all" when more than 50% are checked.
    /// **Validates: Requirements 5.11**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IsAboveHalf_AnyCheckedState_MatchesThresholdRule()
    {
        return Prop.ForAll(
            TotalCountArb(),
            (totalCount) => Prop.ForAll(
                CheckedCountArb(totalCount),
                (checkedCount) =>
                {
                    // Arrange.
                    var expectedAboveHalf = checkedCount > totalCount / 2.0;

                    // Act.
                    var result = IngredientCheckThreshold.IsAboveHalf(totalCount, checkedCount);

                    // Assert.
                    return (result == expectedAboveHalf)
                        .Label($"Expected IsAboveHalf={expectedAboveHalf} but got {result} for totalCount={totalCount}, checkedCount={checkedCount}");
                }));
    }

    /// <summary>
    /// For any non-empty list with exactly half checked (when total is even), the label SHALL be "Check all".
    /// **Validates: Requirements 5.11**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IsAboveHalf_ExactlyHalfChecked_ReturnsFalse()
    {
        return Prop.ForAll(
            EvenTotalCountArb(),
            (totalCount) =>
            {
                // Arrange.
                var checkedCount = totalCount / 2;

                // Act.
                var result = IngredientCheckThreshold.IsAboveHalf(totalCount, checkedCount);

                // Assert.
                return (!result)
                    .Label($"Expected false (Check all) when exactly half ({checkedCount}/{totalCount}) are checked");
            });
    }

    /// <summary>
    /// For any non-empty list with more than half checked, the label SHALL be "Uncheck all".
    /// **Validates: Requirements 5.11**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IsAboveHalf_MoreThanHalfChecked_ReturnsTrue()
    {
        return Prop.ForAll(
            TotalCountArb(),
            (totalCount) => Prop.ForAll(
                MoreThanHalfCheckedArb(totalCount),
                (checkedCount) =>
                {
                    // Act.
                    var result = IngredientCheckThreshold.IsAboveHalf(totalCount, checkedCount);

                    // Assert.
                    return result
                        .Label($"Expected true (Uncheck all) when more than half ({checkedCount}/{totalCount}) are checked");
                }));
    }

    private static Arbitrary<int> TotalCountArb()
    {
        // Generate total ingredient counts between 1 and 30 (max allowed).
        return Arb.From(Gen.Choose(1, RecipeConstants.MaxIngredients));
    }

    private static Arbitrary<int> EvenTotalCountArb()
    {
        // Generate even total counts between 2 and 30.
        return Arb.From(Gen.Choose(1, RecipeConstants.MaxIngredients / 2).Select(x => x * 2));
    }

    private static Arbitrary<int> CheckedCountArb(int totalCount)
    {
        // Generate checked counts between 0 and totalCount.
        return Arb.From(Gen.Choose(0, totalCount));
    }

    private static Arbitrary<int> MoreThanHalfCheckedArb(int totalCount)
    {
        // Generate checked counts that are strictly more than half.
        var minChecked = (int)Math.Floor(totalCount / 2.0) + 1;
        return Arb.From(Gen.Choose(minChecked, totalCount));
    }
}
