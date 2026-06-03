using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Domain;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

// Feature: attendance-slide-in-buttons, Property 15: Click during hover-expansion processes status change and collapses
public class AttendanceRowStateManagerHoverClickPropertyTests
{
    private static readonly Arbitrary<(AttendanceStatus Current, AttendanceStatus New)> StatusChangePairArb =
        Gen.Elements(AttendanceStatus.Unknown, AttendanceStatus.EatingIn, AttendanceStatus.NotEatingIn)
            .SelectMany(x => Gen.Elements(AttendanceStatus.Unknown, AttendanceStatus.EatingIn, AttendanceStatus.NotEatingIn)
                .Where(y => y != x)
                .Select(y => (Current: x, New: y)))
            .ToArbitrary();

    private static readonly Arbitrary<Guid> GuidArb =
        ArbMap.Default.ArbFor<Guid>();

    // Feature: attendance-slide-in-buttons, Property 15: Click during hover-expansion processes status change and collapses
    // Validates: Requirements 12.4
    [Property(MaxTest = 100)]
    public Property HandleExpandedButtonClickAsync_HoverExpanded_DifferentStatus_CollapsesAndReportsStatusChanged()
    {
        return Prop.ForAll(
            GuidArb,
            StatusChangePairArb,
            async (housemateId, statusPair) =>
            {
                // Arrange.
                using var sut = new AttendanceRowStateManager(autoCollapseIntervalMs: 1000, animationDurationMs: 1);
                sut.Configure(isNarrowViewport: true, hasPointerDevice: true);

                // Expand via hover.
                await sut.HandleMouseEnterAsync(housemateId);

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
