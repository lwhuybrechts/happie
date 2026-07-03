using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

// Feature: swipe-preview, Property 2: Rubber-band never exceeds 1.2× viewport width
public class SwipeCarouselMathRubberBandCapPropertyTests
{
    // Viewport widths in [320, 1920].
    private static readonly Arbitrary<double> ViewportWidthArb =
        Gen.Choose(320, 1920)
            .Select(x => (double)x)
            .ToArbitrary();

    // Validates: Requirements 2.4
    [Property(MaxTest = 100)]
    public Property RubberBand_AnyDragDistance_NeverExceedsMaxOvershoot()
    {
        return Prop.ForAll(
            ViewportWidthArb,
            viewportWidth =>
                Prop.ForAll(
                    Gen.Choose(0, (int)(viewportWidth * 10))
                        .SelectMany(x => Gen.Elements(1.0, -1.0).Select(sign => sign * x))
                        .ToArbitrary(),
                    dragDistance =>
                    {
                        var result = SwipeCarouselMath.RubberBand(dragDistance, viewportWidth);
                        var maxAllowed = SwipeCarouselMath.MaxOvershootFactor * viewportWidth;

                        return (Math.Abs(result) <= maxAllowed).Label(
                            $"|RubberBand({dragDistance}, {viewportWidth})| = {Math.Abs(result)}, max allowed = {maxAllowed}");
                    }));
    }
}
