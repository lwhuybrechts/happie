using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Domain;
using Happie.Web.Services;
using Happie.Web.Tests.Helpers;

namespace Happie.Web.Tests.Services;

// Feature: attendance-slide-in-buttons, Property 10: Wide viewport disables all collapse behavior
public class AttendanceRowStateManagerWideViewportPropertyTests
{
    private static readonly Arbitrary<Guid> GuidArb =
        ArbMap.Default.ArbFor<Guid>();

    private static readonly Arbitrary<AttendanceStatus> StatusArb =
        Gen.Elements(AttendanceStatus.EatingIn, AttendanceStatus.NotEatingIn, AttendanceStatus.Unknown)
            .ToArbitrary();

    // Feature: attendance-slide-in-buttons, Property 10: Wide viewport disables all collapse behavior
    // Validates: Requirements 11.1, 11.2, 11.5
    [Property(MaxTest = 100)]
    public Property HandleActiveButtonClickAsync_WideViewport_DoesNotExpand()
    {
        return Prop.ForAll(
            GuidArb,
            async housemateId =>
            {
                // Arrange.
                var sut = CreateWideViewportStateManager();

                // Act.
                await sut.HandleActiveButtonClickAsync(housemateId);

                // Assert.
                return (!sut.IsExpanded(housemateId)
                    && !sut.IsAnimating(housemateId)
                    && !sut.IsCollapseEnabled)
                    .Label($"Wide viewport should not expand row {housemateId}");
            });
    }

    // Feature: attendance-slide-in-buttons, Property 10: Wide viewport disables all collapse behavior
    // Validates: Requirements 11.1, 11.2, 11.5
    [Property(MaxTest = 100)]
    public Property HandleMouseEnterAsync_WideViewport_DoesNotExpand()
    {
        return Prop.ForAll(
            GuidArb,
            async housemateId =>
            {
                // Arrange.
                var sut = CreateWideViewportStateManager();

                // Act.
                await sut.HandleMouseEnterAsync(housemateId);

                // Assert.
                return (!sut.IsExpanded(housemateId)
                    && !sut.IsAnimating(housemateId))
                    .Label($"Wide viewport should not expand row on hover for {housemateId}");
            });
    }

    // Feature: attendance-slide-in-buttons, Property 10: Wide viewport disables all collapse behavior
    // Validates: Requirements 11.1, 11.2, 11.5
    [Property(MaxTest = 100)]
    public Property HandleMouseLeaveAsync_WideViewport_DoesNotAnimate()
    {
        return Prop.ForAll(
            GuidArb,
            async housemateId =>
            {
                // Arrange.
                var sut = CreateWideViewportStateManager();

                // Act.
                await sut.HandleMouseLeaveAsync(housemateId);

                // Assert.
                return (!sut.IsExpanded(housemateId)
                    && !sut.IsAnimating(housemateId))
                    .Label($"Wide viewport should not animate on mouse leave for {housemateId}");
            });
    }

    // Feature: attendance-slide-in-buttons, Property 10: Wide viewport disables all collapse behavior
    // Validates: Requirements 11.1, 11.2, 11.5
    [Property(MaxTest = 100)]
    public Property HandleExpandedButtonClickAsync_WideViewport_DoesNotAnimate()
    {
        return Prop.ForAll(
            GuidArb,
            StatusArb,
            StatusArb,
            async (housemateId, currentStatus, newStatus) =>
            {
                // Arrange.
                var sut = CreateWideViewportStateManager();

                // Act.
                await sut.HandleExpandedButtonClickAsync(housemateId, currentStatus, newStatus);

                // Assert.
                return (!sut.IsExpanded(housemateId)
                    && !sut.IsAnimating(housemateId))
                    .Label($"Wide viewport should not animate on expanded button click for {housemateId}");
            });
    }

    // Feature: attendance-slide-in-buttons, Property 10: Wide viewport disables all collapse behavior
    // Validates: Requirements 11.1, 11.2, 11.5
    [Property(MaxTest = 100)]
    public Property ExpandAsync_WideViewport_DoesNotChangeState()
    {
        return Prop.ForAll(
            GuidArb,
            async housemateId =>
            {
                // Arrange.
                var sut = CreateWideViewportStateManager();

                // Act.
                await sut.ExpandAsync(housemateId);

                // Assert.
                return (!sut.IsExpanded(housemateId)
                    && !sut.IsAnimating(housemateId))
                    .Label($"Wide viewport ExpandAsync should be a no-op for {housemateId}");
            });
    }

    // Feature: attendance-slide-in-buttons, Property 10: Wide viewport disables all collapse behavior
    // Validates: Requirements 11.1, 11.2, 11.5
    [Property(MaxTest = 100)]
    public Property IsCollapseEnabled_WideViewport_AlwaysFalse()
    {
        return Prop.ForAll(
            GuidArb,
            StatusArb,
            (housemateId, status) =>
            {
                // Arrange.
                var sut = CreateWideViewportStateManager();

                // Assert.
                return (!sut.IsCollapseEnabled)
                    .Label("IsCollapseEnabled should be false on wide viewport");
            });
    }

    private static AttendanceRowStateManager CreateWideViewportStateManager()
    {
        var stateManager = new AttendanceRowStateManager(new FakeDelayService());
        // Wide viewport (≥ 480px): isNarrow = false.
        stateManager.Configure(isNarrowViewport: false, hasPointerDevice: true);
        return stateManager;
    }
}
