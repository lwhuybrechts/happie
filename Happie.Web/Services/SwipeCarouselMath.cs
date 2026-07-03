namespace Happie.Web.Services;

/// <summary>
/// Pure C# mirror of the swipe carousel math logic implemented in wwwroot/js/swipeCarousel.js.
/// This class is not called at runtime — the actual touch handling and physics run in JavaScript
/// for 60fps performance. It exists solely as a testable specification of the decision logic,
/// validated by FsCheck property tests. If the JavaScript implementation changes, this class
/// must be updated to match.
/// </summary>
public static class SwipeCarouselMath
{
    public const double SwipeThreshold = 60;
    public const double MaxOvershootFactor = 1.2;

    /// <summary>Applies rubber-band resistance when drag exceeds viewport width.</summary>
    public static double RubberBand(double dragDistance, double viewportWidth)
    {
        var abs = Math.Abs(dragDistance);
        if (abs <= viewportWidth)
            return dragDistance;

        var over = abs - viewportWidth;
        var maxOvershoot = viewportWidth * (MaxOvershootFactor - 1.0);
        var dampened = viewportWidth + maxOvershoot * (1 - Math.Exp(-over / viewportWidth));
        return dragDistance > 0 ? dampened : -dampened;
    }

    /// <summary>Determines if a touch start is in an edge exclusion zone.</summary>
    public static bool IsInEdgeExclusionZone(double clientX, double viewportWidth)
    {
        return clientX <= 20 || clientX >= viewportWidth - 20;
    }

    /// <summary>Determines the direction lock based on first axis to exceed 10px.</summary>
    public static bool? DetermineDirectionLock(double deltaX, double deltaY)
    {
        if (Math.Abs(deltaX) < 10 && Math.Abs(deltaY) < 10)
            return null;

        return Math.Abs(deltaX) >= Math.Abs(deltaY);
    }

    /// <summary>Determines swipe outcome: true = navigate, false = snap-back.</summary>
    public static bool ShouldNavigate(double dragDistance)
    {
        return Math.Abs(dragDistance) >= SwipeThreshold;
    }
}
