using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Domain;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

// Feature: attendance-slide-in-buttons, Property 3: Animation lock prevents all interaction
public class AttendanceRowStateManagerAnimationLockPropertyTests
{
    private static readonly Arbitrary<AttendanceStatus> StatusArb =
        Gen.Elements(AttendanceStatus.EatingIn, AttendanceStatus.NotEatingIn, AttendanceStatus.Unknown)
            .ToArbitrary();

    private static readonly Arbitrary<(Guid RowA, Guid RowB)> TwoDistinctGuidArb =
        ArbMap.Default.ArbFor<Guid>().Generator
            .Two()
            .Where(x => x.Item1 != x.Item2)
            .Select(x => (RowA: x.Item1, RowB: x.Item2))
            .ToArbitrary();

    // Feature: attendance-slide-in-buttons, Property 3: Animation lock prevents all interaction
    // Validates: Requirements 2.6, 5.3, 7.4
    [Property(MaxTest = 100)]
    public Property HandleActiveButtonClickAsync_AnimatingRow_DoesNotChangeState()
    {
        return Prop.ForAll(
            TwoDistinctGuidArb,
            async guids =>
            {
                // Arrange.
                using var sut = new AttendanceRowStateManager(autoCollapseIntervalMs: 1000, animationDurationMs: 1);
                sut.Configure(isNarrowViewport: true, hasPointerDevice: false);

                // Expand row A fully (animation completes after 250ms await).
                await sut.ExpandAsync(guids.RowA);

                // Start expanding row B without awaiting — this synchronously puts row A
                // into _animatingIds (single-row collapse) before yielding at Task.Delay.
                var expandTask = sut.ExpandAsync(guids.RowB);

                // Row A should be animating now.
                var isAnimatingBeforeClick = sut.IsAnimating(guids.RowA);

                // Act: attempt to click the active button on the animating row A.
                await sut.HandleActiveButtonClickAsync(guids.RowA);

                // Assert: row A should not have been expanded — animation lock prevented it.
                var rowANotExpanded = !sut.IsExpanded(guids.RowA);
                var rowBStillExpanded = sut.IsExpanded(guids.RowB);

                await expandTask;

                return (isAnimatingBeforeClick && rowANotExpanded && rowBStillExpanded)
                    .Label($"rowA={guids.RowA}, rowB={guids.RowB}: isAnimating={isAnimatingBeforeClick}, rowANotExpanded={rowANotExpanded}, rowBExpanded={rowBStillExpanded}");
            });
    }

    // Feature: attendance-slide-in-buttons, Property 3: Animation lock prevents all interaction
    // Validates: Requirements 2.6, 5.3, 7.4
    [Property(MaxTest = 100)]
    public Property HandleExpandedButtonClickAsync_AnimatingRow_DoesNotChangeState()
    {
        return Prop.ForAll(
            TwoDistinctGuidArb,
            StatusArb,
            async (guids, newStatus) =>
            {
                // Arrange.
                using var sut = new AttendanceRowStateManager(autoCollapseIntervalMs: 1000, animationDurationMs: 1);
                sut.Configure(isNarrowViewport: true, hasPointerDevice: false);

                // Expand row A fully.
                await sut.ExpandAsync(guids.RowA);

                // Start expanding row B — puts row A into animating state synchronously.
                var expandTask = sut.ExpandAsync(guids.RowB);

                var isAnimatingBeforeClick = sut.IsAnimating(guids.RowA);
                var expandedBefore = sut.IsExpanded(guids.RowB);

                // Act: attempt expanded button click on the animating row A.
                var result = await sut.HandleExpandedButtonClickAsync(guids.RowA, AttendanceStatus.Unknown, newStatus);

                // Assert: row B's expanded state should be unchanged (animation lock on A blocked the call).
                var expandedAfter = sut.IsExpanded(guids.RowB);
                var statePreserved = expandedBefore == expandedAfter;
                var rowANotExpanded = !sut.IsExpanded(guids.RowA);

                await expandTask;

                return (isAnimatingBeforeClick && rowANotExpanded && statePreserved)
                    .Label($"rowA={guids.RowA}: isAnimating={isAnimatingBeforeClick}, rowANotExpanded={rowANotExpanded}, statePreserved={statePreserved}");
            });
    }

    // Feature: attendance-slide-in-buttons, Property 3: Animation lock prevents all interaction
    // Validates: Requirements 2.6, 5.3, 7.4
    [Property(MaxTest = 100)]
    public Property HandleMouseEnterAsync_AnimatingRow_DoesNotChangeState()
    {
        return Prop.ForAll(
            TwoDistinctGuidArb,
            async guids =>
            {
                // Arrange.
                using var sut = new AttendanceRowStateManager(autoCollapseIntervalMs: 1000, animationDurationMs: 1);
                sut.Configure(isNarrowViewport: true, hasPointerDevice: true);

                // Expand row A fully.
                await sut.ExpandAsync(guids.RowA);

                // Start expanding row B — puts row A into animating state.
                var expandTask = sut.ExpandAsync(guids.RowB);

                var isAnimatingBeforeHover = sut.IsAnimating(guids.RowA);

                // Act: attempt mouse enter on the animating row A.
                await sut.HandleMouseEnterAsync(guids.RowA);

                // Assert: row A should not be expanded (animation lock prevented it).
                var rowANotExpanded = !sut.IsExpanded(guids.RowA);

                await expandTask;

                return (isAnimatingBeforeHover && rowANotExpanded)
                    .Label($"rowA={guids.RowA}: isAnimating={isAnimatingBeforeHover}, rowANotExpanded={rowANotExpanded}");
            });
    }

    // Feature: attendance-slide-in-buttons, Property 3: Animation lock prevents all interaction
    // Validates: Requirements 2.6, 5.3, 7.4
    [Property(MaxTest = 100)]
    public Property HandleMouseLeaveAsync_AnimatingRow_DoesNotChangeState()
    {
        return Prop.ForAll(
            TwoDistinctGuidArb,
            async guids =>
            {
                // Arrange.
                using var sut = new AttendanceRowStateManager(autoCollapseIntervalMs: 1000, animationDurationMs: 1);
                sut.Configure(isNarrowViewport: true, hasPointerDevice: true);

                // Expand row A via hover.
                await sut.HandleMouseEnterAsync(guids.RowA);

                // Start expanding row B — puts row A into animating state.
                var expandTask = sut.ExpandAsync(guids.RowB);

                var isAnimatingBeforeLeave = sut.IsAnimating(guids.RowA);
                var expandedBefore = sut.IsExpanded(guids.RowB);

                // Act: attempt mouse leave on the animating row A.
                await sut.HandleMouseLeaveAsync(guids.RowA);

                // Assert: state should remain unchanged (animation lock blocked the leave).
                var expandedAfter = sut.IsExpanded(guids.RowB);
                var statePreserved = expandedBefore == expandedAfter;

                await expandTask;

                return (isAnimatingBeforeLeave && statePreserved)
                    .Label($"rowA={guids.RowA}: isAnimating={isAnimatingBeforeLeave}, statePreserved={statePreserved}");
            });
    }

    // Feature: attendance-slide-in-buttons, Property 3: Animation lock prevents all interaction
    // Validates: Requirements 2.6, 5.3, 7.4
    [Property(MaxTest = 100)]
    public Property HandleOutsideClickAsync_AnimatingRow_DoesNotChangeState()
    {
        return Prop.ForAll(
            TwoDistinctGuidArb,
            async guids =>
            {
                // Arrange: use a longer animation duration to prevent the race condition where
                // Task.Delay(1) completes before the test can observe the animating state.
                using var sut = new AttendanceRowStateManager(autoCollapseIntervalMs: 1000, animationDurationMs: 100);
                sut.Configure(isNarrowViewport: true, hasPointerDevice: false);

                // Expand row A fully.
                await sut.ExpandAsync(guids.RowA);

                // Start expanding row B — this sets row B as expanded and puts it into animating state.
                var expandTask = sut.ExpandAsync(guids.RowB);

                // Row B is the currently expanded row and it's animating.
                var isRowBAnimating = sut.IsAnimating(guids.RowB);
                var rowBExpandedBefore = sut.IsExpanded(guids.RowB);

                // Act: attempt outside click while the expanded row (B) is animating.
                await sut.HandleOutsideClickAsync();

                // Assert: row B should still be expanded (outside click blocked by animation lock).
                var rowBExpandedAfter = sut.IsExpanded(guids.RowB);
                var statePreserved = rowBExpandedBefore == rowBExpandedAfter;

                await expandTask;

                return (isRowBAnimating && statePreserved)
                    .Label($"rowB={guids.RowB}: isAnimating={isRowBAnimating}, expandedBefore={rowBExpandedBefore}, expandedAfter={rowBExpandedAfter}");
            });
    }
}
