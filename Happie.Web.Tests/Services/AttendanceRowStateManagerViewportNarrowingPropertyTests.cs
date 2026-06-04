using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services;
using Happie.Web.Tests.Helpers;

namespace Happie.Web.Tests.Services;

// Feature: attendance-slide-in-buttons, Property 11: Viewport narrowing collapses all rows
public class AttendanceRowStateManagerViewportNarrowingPropertyTests
{
    private static readonly Arbitrary<Guid> GuidArb =
        ArbMap.Default.ArbFor<Guid>();

    // Feature: attendance-slide-in-buttons, Property 11: Viewport narrowing collapses all rows
    // Validates: Requirements 11.4
    [Property(MaxTest = 100)]
    public Property HandleViewportChangeAsync_NarrowingFromWide_CollapsesExpandedRow()
    {
        return Prop.ForAll(
            GuidArb,
            async housemateId =>
            {
                // Arrange.
                var sut = new AttendanceRowStateManager(new FakeDelayService());
                sut.Configure(isNarrowViewport: true, hasPointerDevice: false);

                // Expand a row in narrow viewport.
                await sut.ExpandAsync(housemateId);

                // Switch to wide viewport.
                await sut.HandleViewportChangeAsync(isNarrow: false);

                // Act: switch back to narrow viewport (the transition under test).
                await sut.HandleViewportChangeAsync(isNarrow: true);

                // Assert: no row is expanded and the auto-collapse timer is not active.
                return (!sut.IsExpanded(housemateId)
                    && !sut.IsAutoCollapseTimerActive)
                    .Label($"Viewport narrowing should collapse all rows and cancel timer for {housemateId}");
            });
    }
}
