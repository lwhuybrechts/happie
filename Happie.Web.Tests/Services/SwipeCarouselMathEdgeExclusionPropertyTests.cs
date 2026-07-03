using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

// Feature: swipe-preview, Property 6: Edge exclusion rejects touches within 20px of viewport edges
public class SwipeCarouselMathEdgeExclusionPropertyTests
{
    private static readonly Arbitrary<(double ClientX, double ViewportWidth)> EdgeTouchArb =
        Gen.Choose(320, 1920)
            .SelectMany(x =>
            {
                var viewportWidth = (double)x;
                // Generate clientX in [0, 20] or [viewportWidth - 20, viewportWidth].
                var leftEdge = Gen.Choose(0, 20).Select(c => (double)c);
                var rightEdge = Gen.Choose((int)(viewportWidth - 20), (int)viewportWidth).Select(c => (double)c);
                return Gen.OneOf(leftEdge, rightEdge).Select(c => (ClientX: c, ViewportWidth: viewportWidth));
            })
            .ToArbitrary();

    private static readonly Arbitrary<(double ClientX, double ViewportWidth)> CenterTouchArb =
        Gen.Choose(320, 1920)
            .SelectMany(x =>
            {
                var viewportWidth = (double)x;
                // Generate clientX in (20, viewportWidth - 20).
                return Gen.Choose(21, (int)(viewportWidth - 21)).Select(c => (ClientX: (double)c, ViewportWidth: viewportWidth));
            })
            .ToArbitrary();

    // Feature: swipe-preview, Property 6: Edge exclusion rejects touches within 20px of viewport edges
    // Validates: Requirements 9.1
    [Property(MaxTest = 100)]
    public Property IsInEdgeExclusionZone_TouchInEdge_ReturnsTrue()
    {
        return Prop.ForAll(
            EdgeTouchArb,
            x =>
                SwipeCarouselMath.IsInEdgeExclusionZone(x.ClientX, x.ViewportWidth)
                    .Label($"Expected true for clientX={x.ClientX}, viewportWidth={x.ViewportWidth}"));
    }

    // Feature: swipe-preview, Property 6: Edge exclusion rejects touches within 20px of viewport edges
    // Validates: Requirements 9.1
    [Property(MaxTest = 100)]
    public Property IsInEdgeExclusionZone_TouchInCenter_ReturnsFalse()
    {
        return Prop.ForAll(
            CenterTouchArb,
            x =>
                (!SwipeCarouselMath.IsInEdgeExclusionZone(x.ClientX, x.ViewportWidth))
                    .Label($"Expected false for clientX={x.ClientX}, viewportWidth={x.ViewportWidth}"));
    }
}
