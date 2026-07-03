using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

// Feature: swipe-preview, Property 3: Direction lock decision is consistent with first axis to exceed 10px
public class SwipeCarouselMathDirectionLockPropertyTests
{
    private static readonly Arbitrary<(double DeltaX, double DeltaY)> BothBelowThresholdArb =
        Gen.Choose(-99, 99)
            .Select(x => x / 10.0)
            .SelectMany(x => Gen.Choose(-99, 99).Select(y => (DeltaX: x, DeltaY: y / 10.0)))
            .Where(x => Math.Abs(x.DeltaX) < 10 && Math.Abs(x.DeltaY) < 10)
            .ToArbitrary();

    private static readonly Arbitrary<(double DeltaX, double DeltaY)> AtLeastOneExceedsThresholdArb =
        Gen.Choose(-5000, 5000)
            .Select(x => x / 10.0)
            .SelectMany(x => Gen.Choose(-5000, 5000).Select(y => (DeltaX: x, DeltaY: y / 10.0)))
            .Where(x => Math.Abs(x.DeltaX) >= 10 || Math.Abs(x.DeltaY) >= 10)
            .ToArbitrary();

    // Validates: Requirements 2.5, 2.6
    [Property(MaxTest = 100)]
    public Property DetermineDirectionLock_BothAxesBelowThreshold_ReturnsNull()
    {
        return Prop.ForAll(
            BothBelowThresholdArb,
            x =>
                (SwipeCarouselMath.DetermineDirectionLock(x.DeltaX, x.DeltaY) == null)
                    .Label($"Expected null for deltaX={x.DeltaX}, deltaY={x.DeltaY}"));
    }

    // Validates: Requirements 2.5, 2.6
    [Property(MaxTest = 100)]
    public Property DetermineDirectionLock_AtLeastOneAxisExceedsThreshold_MatchesLargerAxis()
    {
        return Prop.ForAll(
            AtLeastOneExceedsThresholdArb,
            x =>
            {
                var result = SwipeCarouselMath.DetermineDirectionLock(x.DeltaX, x.DeltaY);
                var expected = Math.Abs(x.DeltaX) >= Math.Abs(x.DeltaY);

                return (result == expected)
                    .Label($"Expected {expected} for deltaX={x.DeltaX}, deltaY={x.DeltaY}, got {result}");
            });
    }
}
