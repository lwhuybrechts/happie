using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services;
using Happie.Web.Tests.Helpers;

namespace Happie.Web.Tests.Services;

// Feature: attendance-slide-in-buttons, Property 13: Mouse-leave collapses hover-expanded row
public class AttendanceRowStateManagerMouseLeavePropertyTests
{
    // Feature: attendance-slide-in-buttons, Property 13: Mouse-leave collapses hover-expanded row
    // Validates: Requirements 12.2
    [Property(MaxTest = 100)]
    public Property HandleMouseLeaveAsync_HoverExpandedRow_CollapsesRow()
    {
        return Prop.ForAll(
            ArbMap.Default.ArbFor<Guid>(),
            async housemateId =>
            {
                // Arrange.
                var sut = new AttendanceRowStateManager(new FakeDelayService());
                sut.Configure(isNarrowViewport: true, hasPointerDevice: true);

                // Act — expand via hover.
                await sut.HandleMouseEnterAsync(housemateId);

                // Verify expanded.
                var expandedAfterHover = sut.IsExpanded(housemateId);

                // Act — mouse-leave.
                await sut.HandleMouseLeaveAsync(housemateId);

                // Assert — row is no longer expanded.
                var collapsedAfterLeave = !sut.IsExpanded(housemateId);

                return (expandedAfterHover && collapsedAfterLeave)
                    .Label($"Expected row {housemateId} to collapse after mouse-leave (ExpandedAfterHover={expandedAfterHover}, CollapsedAfterLeave={collapsedAfterLeave})");
            });
    }
}
