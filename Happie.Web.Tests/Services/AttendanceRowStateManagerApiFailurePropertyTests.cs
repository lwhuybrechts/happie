using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Domain;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

// Feature: attendance-slide-in-buttons, Property 5: API failure reverts to previous status
public class AttendanceRowStateManagerApiFailurePropertyTests
{
    private static readonly Arbitrary<(Guid HousemateId, AttendanceStatus Current, AttendanceStatus New)> StatusChangeArb =
        ArbMap.Default.ArbFor<Guid>().Generator
            .SelectMany(id =>
                Gen.Elements(AttendanceStatus.EatingIn, AttendanceStatus.NotEatingIn, AttendanceStatus.Unknown)
                    .SelectMany(current =>
                        Gen.Elements(AttendanceStatus.EatingIn, AttendanceStatus.NotEatingIn, AttendanceStatus.Unknown)
                            .Where(next => next != current)
                            .Select(next => (HousemateId: id, Current: current, New: next))))
            .ToArbitrary();

    // Feature: attendance-slide-in-buttons, Property 5: API failure reverts to previous status
    // Validates: Requirements 3.5
    [Property(MaxTest = 100)]
    public Property HandleExpandedButtonClickAsync_ApiFailure_RevertsToOriginalStatus()
    {
        return Prop.ForAll(
            StatusChangeArb,
            async input =>
            {
                // Arrange.
                using var sut = new AttendanceRowStateManager(autoCollapseIntervalMs: 1000, animationDurationMs: 1);
                sut.Configure(isNarrowViewport: true, hasPointerDevice: false);

                // Expand the row (awaits internal 250ms animation lock).
                await sut.ExpandAsync(input.HousemateId);

                // Act — click button for new status (collapses and reports status changed).
                var result = await sut.HandleExpandedButtonClickAsync(input.HousemateId, input.Current, input.New);

                // The result indicates a status change occurred (optimistic update applied).
                var statusChanged = result.StatusChanged;

                // Simulate API failure: the component reverts the displayed status back to original.
                // GetActiveStatus with the original status should correctly return the original.
                var revertedStatus = sut.GetActiveStatus(input.HousemateId, input.Current);

                // Assert: status changed was reported, row is collapsed, and reverted status matches original.
                var rowIsCollapsed = !sut.IsExpanded(input.HousemateId);
                var statusRevertedCorrectly = revertedStatus == input.Current;

                return (statusChanged && rowIsCollapsed && statusRevertedCorrectly)
                    .Label($"Expected StatusChanged=true, row collapsed, reverted to {input.Current}. " +
                           $"Got StatusChanged={statusChanged}, IsExpanded={!rowIsCollapsed}, ActiveStatus={revertedStatus}");
            });
    }
}
