using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Domain;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

// Feature: attendance-slide-in-buttons, Property 9: Auto-collapse timer lifecycle matches row state
public class AttendanceRowStateManagerTimerPropertyTests
{
    private static readonly Arbitrary<AttendanceStatus> AttendanceStatusArb =
        Gen.Elements(AttendanceStatus.Unknown, AttendanceStatus.EatingIn, AttendanceStatus.NotEatingIn)
            .ToArbitrary();

    // Feature: attendance-slide-in-buttons, Property 9: Auto-collapse timer lifecycle matches row state
    // Validates: Requirements 10.1
    [Property(MaxTest = 100)]
    public Property ExpandAsync_NarrowViewport_StartsAutoCollapseTimer()
    {
        return Prop.ForAll(
            ArbMap.Default.ArbFor<Guid>(),
            async housemateId =>
            {
                // Arrange.
                using var sut = CreateSut();

                // Act.
                await sut.ExpandAsync(housemateId);

                // Assert.
                return sut.IsAutoCollapseTimerActive
                    .Label($"Expected auto-collapse timer to be active after expanding row {housemateId}");
            });
    }

    // Feature: attendance-slide-in-buttons, Property 9: Auto-collapse timer lifecycle matches row state
    // Validates: Requirements 10.3
    [Property(MaxTest = 100)]
    public Property HandleExpandedButtonClickAsync_WhileExpanded_CancelsTimer()
    {
        return Prop.ForAll(
            ArbMap.Default.ArbFor<Guid>(),
            AttendanceStatusArb,
            async (housemateId, currentStatus) =>
            {
                // Arrange.
                using var sut = CreateSut();
                await sut.ExpandAsync(housemateId);

                // Wait for animation lock to clear.
                await Task.Delay(50);

                // Pick a different status for the click.
                var newStatus = currentStatus == AttendanceStatus.EatingIn
                    ? AttendanceStatus.NotEatingIn
                    : AttendanceStatus.EatingIn;

                // Act.
                await sut.HandleExpandedButtonClickAsync(housemateId, currentStatus, newStatus);

                // Assert.
                return (!sut.IsAutoCollapseTimerActive)
                    .Label($"Expected auto-collapse timer to be cancelled after button click for row {housemateId}");
            });
    }

    // Feature: attendance-slide-in-buttons, Property 9: Auto-collapse timer lifecycle matches row state
    // Validates: Requirements 10.4
    [Property(MaxTest = 100)]
    public Property CollapseAsync_ForAnyReason_CancelsTimer()
    {
        return Prop.ForAll(
            ArbMap.Default.ArbFor<Guid>(),
            async housemateId =>
            {
                // Arrange.
                using var sut = CreateSut();
                await sut.ExpandAsync(housemateId);

                // Wait for animation lock to clear.
                await Task.Delay(50);

                // Act.
                await sut.CollapseAsync(housemateId);

                // Assert.
                return (!sut.IsAutoCollapseTimerActive)
                    .Label($"Expected auto-collapse timer to be cancelled after collapsing row {housemateId}");
            });
    }

    // Feature: attendance-slide-in-buttons, Property 9: Auto-collapse timer lifecycle matches row state
    // Validates: Requirements 10.2
    [Property(MaxTest = 100)]
    public Property TimerExpiry_WithoutInteraction_CollapsesRow()
    {
        return Prop.ForAll(
            ArbMap.Default.ArbFor<Guid>(),
            async housemateId =>
            {
                // Arrange: use a short timer interval that fires shortly after animation completes.
                using var sut = CreateSut(autoCollapseIntervalMs: 50);
                await sut.ExpandAsync(housemateId);

                // Wait for timer to fire + buffer.
                await Task.Delay(100);

                // Assert.
                return (!sut.IsExpanded(housemateId))
                    .Label($"Expected row {housemateId} to be collapsed after timer expiry");
            });
    }

    private static AttendanceRowStateManager CreateSut(int autoCollapseIntervalMs = 1000)
    {
        var sut = new AttendanceRowStateManager(autoCollapseIntervalMs, animationDurationMs: 1);
        sut.Configure(isNarrowViewport: true, hasPointerDevice: false);
        return sut;
    }
}
