using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

// Feature: swipe-preview, Property 4: Swipe outcome determined by threshold comparison
// Validates: Requirements 3.1, 4.1
public class SwipeCarouselMathThresholdPropertyTests
{
    private static readonly Arbitrary<double> AboveThresholdArb =
        Gen.Choose(6000, 30000)
            .Select(x => x / 100.0)
            .ToArbitrary();

    private static readonly Arbitrary<double> BelowThresholdArb =
        Gen.Choose(0, 5999)
            .Select(x => x / 100.0)
            .ToArbitrary();

    private static readonly Arbitrary<bool> SignArb =
        Gen.Elements(true, false)
            .ToArbitrary();

    [Property(MaxTest = 100)]
    public Property ShouldNavigate_AboveThreshold_ReturnsTrue() =>
        Prop.ForAll(
            AboveThresholdArb,
            SignArb,
            (x, positive) =>
                SwipeCarouselMath.ShouldNavigate(positive ? x : -x)
                    .Label($"Expected navigate=true for drag distance {(positive ? x : -x)}"));

    [Property(MaxTest = 100)]
    public Property ShouldNavigate_BelowThreshold_ReturnsFalse() =>
        Prop.ForAll(
            BelowThresholdArb,
            SignArb,
            (x, positive) =>
                (!SwipeCarouselMath.ShouldNavigate(positive ? x : -x))
                    .Label($"Expected navigate=false for drag distance {(positive ? x : -x)}"));
}
