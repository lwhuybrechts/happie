using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Domain;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

// Feature: attendance-slide-in-buttons, Property 4: Status change collapses and applies new status optimistically
public class AttendanceRowStateManagerPropertyTests
{
    private static readonly Arbitrary<(AttendanceStatus Current, AttendanceStatus New)> StatusChangePairArb =
        Gen.Elements(AttendanceStatus.Unknown, AttendanceStatus.EatingIn, AttendanceStatus.NotEatingIn)
            .SelectMany(x => Gen.Elements(AttendanceStatus.Unknown, AttendanceStatus.EatingIn, AttendanceStatus.NotEatingIn)
                .Where(y => y != x)
                .Select(y => (Current: x, New: y)))
            .ToArbitrary();

    private static readonly Arbitrary<Guid> GuidArb =
        ArbMap.Default.ArbFor<Guid>();

    // Feature: attendance-slide-in-buttons, Property 4: Status change collapses and applies new status optimistically
    // Validates: Requirements 3.1, 3.2
    [Property(MaxTest = 100)]
    public Property HandleExpandedButtonClickAsync_DifferentStatus_CollapsesAndReportsStatusChanged()
    {
        return Prop.ForAll(
            GuidArb,
            StatusChangePairArb,
            async (housemateId, statusPair) =>
            {
                // Arrange.
                using var sut = new AttendanceRowStateManager(autoCollapseIntervalMs: 1000, animationDurationMs: 1);
                sut.Configure(isNarrowViewport: true, hasPointerDevice: false);

                // Expand the row first (ExpandAsync awaits the 250ms animation lock internally).
                await sut.ExpandAsync(housemateId);

                // Act.
                var result = await sut.HandleExpandedButtonClickAsync(housemateId, statusPair.Current, statusPair.New);

                // Assert.
                var isCollapsed = !sut.IsExpanded(housemateId);
                var statusChanged = result.StatusChanged;

                return (isCollapsed && statusChanged)
                    .Label($"Expected collapsed=true, statusChanged=true for {statusPair.Current} -> {statusPair.New}, got collapsed={isCollapsed}, statusChanged={statusChanged}");
            });
    }
}
