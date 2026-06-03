using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

// Feature: attendance-slide-in-buttons, Property 12: Hover expands row on pointer devices in narrow viewport
public class AttendanceRowStateManagerHoverPropertyTests
{
    // Feature: attendance-slide-in-buttons, Property 12: Hover expands row on pointer devices in narrow viewport
    // Validates: Requirements 12.1
    [Property(MaxTest = 100)]
    public Property HandleMouseEnterAsync_PointerDeviceNarrowViewport_ExpandsRowViaHover()
    {
        return Prop.ForAll(
            ArbMap.Default.ArbFor<Guid>(),
            async housemateId =>
            {
                // Arrange.
                var sut = new AttendanceRowStateManager(autoCollapseIntervalMs: 1000, animationDurationMs: 1);
                sut.Configure(isNarrowViewport: true, hasPointerDevice: true);

                // Act.
                await sut.HandleMouseEnterAsync(housemateId);

                // Assert — row is expanded.
                var isExpanded = sut.IsExpanded(housemateId);

                // Assert — expandedViaHover is true by verifying that mouse-leave collapses the row.
                await sut.HandleMouseLeaveAsync(housemateId);
                var collapsedAfterMouseLeave = !sut.IsExpanded(housemateId);

                return (isExpanded && collapsedAfterMouseLeave)
                    .Label($"Expected row {housemateId} to be expanded via hover (IsExpanded={isExpanded}, CollapsedAfterMouseLeave={collapsedAfterMouseLeave})");
            });
    }
}
