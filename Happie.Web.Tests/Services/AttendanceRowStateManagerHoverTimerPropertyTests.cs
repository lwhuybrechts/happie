using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services;
using Happie.Web.Tests.Helpers;

namespace Happie.Web.Tests.Services;

// Feature: attendance-slide-in-buttons, Property 14: Hover expansion defers auto-collapse timer
public class AttendanceRowStateManagerHoverTimerPropertyTests
{
    // Feature: attendance-slide-in-buttons, Property 14: Hover expansion defers auto-collapse timer
    // Validates: Requirements 12.3
    [Property(MaxTest = 100)]
    public Property HandleMouseEnterAsync_ExpandsViaHover_DoesNotStartAutoCollapseTimer()
    {
        return Prop.ForAll(
            ArbMap.Default.ArbFor<Guid>(),
            async housemateId =>
            {
                // Arrange.
                using var sut = CreateSut();

                // Act.
                await sut.HandleMouseEnterAsync(housemateId);

                // Assert.
                return (sut.IsExpanded(housemateId) && !sut.IsAutoCollapseTimerActive)
                    .Label($"Expected row {housemateId} to be expanded via hover with NO auto-collapse timer active");
            });
    }

    private static AttendanceRowStateManager CreateSut()
    {
        var sut = new AttendanceRowStateManager(new FakeDelayService());
        sut.Configure(isNarrowViewport: true, hasPointerDevice: true);
        return sut;
    }
}
