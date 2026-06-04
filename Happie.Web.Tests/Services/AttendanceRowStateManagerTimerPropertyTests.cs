using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Domain;
using Happie.Web.Services;
using Happie.Web.Tests.Helpers;

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
                var (sut, _) = CreateSut();
                using var disposable = sut;

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
                var (sut, _) = CreateSut();
                using var disposable = sut;
                await sut.ExpandAsync(housemateId);

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
                var (sut, _) = CreateSut();
                using var disposable = sut;
                await sut.ExpandAsync(housemateId);

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
                // Arrange.
                var (sut, fakeDelay) = CreateSut();
                using var disposable = sut;
                await sut.ExpandAsync(housemateId);

                // Act: trigger the timer deterministically.
                await fakeDelay.TriggerTimerAsync();

                // Assert.
                return (!sut.IsExpanded(housemateId))
                    .Label($"Expected row {housemateId} to be collapsed after timer expiry");
            });
    }

    private static (AttendanceRowStateManager Sut, FakeDelayService FakeDelay) CreateSut()
    {
        var fakeDelay = new FakeDelayService();
        var sut = new AttendanceRowStateManager(fakeDelay);
        sut.Configure(isNarrowViewport: true, hasPointerDevice: false);
        return (sut, fakeDelay);
    }
}
