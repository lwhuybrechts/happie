using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

// Feature: swipe-preview, Property 1: Linear translation matches drag distance
public class SwipeCarouselMathLinearTranslationPropertyTests
{
    // Generate viewport widths in [320, 1920].
    private static readonly Arbitrary<double> ViewportWidthArb =
        Gen.Choose(320, 1920)
            .Select(x => (double)x)
            .ToArbitrary();

    // Validates: Requirements 2.1
    [Property(MaxTest = 100)]
    public Property RubberBand_LinearRange_OutputEqualsDragDistance()
    {
        return Prop.ForAll(
            ViewportWidthArb,
            viewportWidth =>
            {
                // Generate drag distances in [0, viewportWidth].
                var dragDistanceArb = Gen.Choose(0, (int)viewportWidth)
                    .Select(x => (double)x)
                    .ToArbitrary();

                return Prop.ForAll(
                    dragDistanceArb,
                    dragDistance =>
                    {
                        var result = SwipeCarouselMath.RubberBand(dragDistance, viewportWidth);

                        return (result == dragDistance).Label(
                            $"Expected RubberBand({dragDistance}, {viewportWidth}) == {dragDistance}, got {result}");
                    });
            });
    }
}
