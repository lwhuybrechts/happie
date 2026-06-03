using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Domain;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

// Feature: attendance-slide-in-buttons, Property 6: Collapse without status change preserves status and sends no API request.
public class AttendanceRowStateManagerCollapsePreservesStatusPropertyTests
{
    private static readonly Arbitrary<AttendanceStatus> StatusArb =
        Gen.Elements(AttendanceStatus.EatingIn, AttendanceStatus.NotEatingIn, AttendanceStatus.Unknown)
            .ToArbitrary();

    private static readonly Arbitrary<Guid> GuidArb =
        Gen.Choose(1, int.MaxValue)
            .Select(x => new Guid(x, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0))
            .ToArbitrary();

    // Feature: attendance-slide-in-buttons, Property 6: Collapse without status change preserves status and sends no API request.
    // Validates: Requirements 4.2, 5.1, 5.2
    [Property(MaxTest = 100)]
    public Property HandleExpandedButtonClickAsync_SameStatus_CollapsesWithoutStatusChange()
    {
        return Prop.ForAll(
            GuidArb,
            StatusArb,
            async (housemateId, currentStatus) =>
            {
                // Arrange.
                using var sut = new AttendanceRowStateManager(autoCollapseIntervalMs: 1000, animationDurationMs: 1);
                sut.Configure(isNarrowViewport: true, hasPointerDevice: false);

                // Expand the row (ExpandAsync awaits the 250ms animation lock internally).
                await sut.ExpandAsync(housemateId);

                // Act: re-tap the same status button.
                var result = await sut.HandleExpandedButtonClickAsync(housemateId, currentStatus, currentStatus);

                // Assert.
                var isCollapsed = !sut.IsExpanded(housemateId);
                var noStatusChange = !result.StatusChanged;
                var activeStatusPreserved = sut.GetActiveStatus(housemateId, currentStatus) == currentStatus;

                return (isCollapsed && noStatusChange && activeStatusPreserved)
                    .Label($"Re-tap status={currentStatus}: collapsed={isCollapsed}, statusChanged={result.StatusChanged}, activePreserved={activeStatusPreserved}");
            });
    }

    // Feature: attendance-slide-in-buttons, Property 6: Collapse without status change preserves status and sends no API request.
    // Validates: Requirements 4.2, 5.1, 5.2
    [Property(MaxTest = 100)]
    public Property HandleOutsideClickAsync_ExpandedRow_CollapsesWithoutStatusChange()
    {
        return Prop.ForAll(
            GuidArb,
            StatusArb,
            async (housemateId, currentStatus) =>
            {
                // Arrange.
                using var sut = new AttendanceRowStateManager(autoCollapseIntervalMs: 1000, animationDurationMs: 1);
                sut.Configure(isNarrowViewport: true, hasPointerDevice: false);

                // Expand the row (ExpandAsync awaits the 250ms animation lock internally).
                await sut.ExpandAsync(housemateId);

                // Act: outside click collapses.
                await sut.HandleOutsideClickAsync();

                // Assert.
                var isCollapsed = !sut.IsExpanded(housemateId);
                var activeStatusPreserved = sut.GetActiveStatus(housemateId, currentStatus) == currentStatus;

                return (isCollapsed && activeStatusPreserved)
                    .Label($"Outside click status={currentStatus}: collapsed={isCollapsed}, activePreserved={activeStatusPreserved}");
            });
    }
}
