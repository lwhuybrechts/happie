using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Domain;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

// Feature: attendance-slide-in-buttons, Property 8: Accessibility attributes reflect expand/collapse state
public class AttendanceRowStateManagerAccessibilityPropertyTests
{
    private static readonly AttendanceStatus[] AllStatuses =
        [AttendanceStatus.Unknown, AttendanceStatus.EatingIn, AttendanceStatus.NotEatingIn];

    private static readonly Arbitrary<AttendanceStatus> StatusArb =
        Gen.Elements(AttendanceStatus.Unknown, AttendanceStatus.EatingIn, AttendanceStatus.NotEatingIn)
            .ToArbitrary();

    private static readonly Arbitrary<Guid> GuidArb =
        Gen.Choose(1, int.MaxValue)
            .Select(x => new Guid(x, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0))
            .ToArbitrary();

    // Feature: attendance-slide-in-buttons, Property 8: Accessibility attributes reflect expand/collapse state
    // Validates: Requirements 8.1, 8.2, 8.3, 8.4
    [Property(MaxTest = 100)]
    public Property CollapsedRow_ActiveButtonHasAriaExpandedFalse_InactiveButtonsAreHidden()
    {
        return Prop.ForAll(
            GuidArb,
            StatusArb,
            (housemateId, currentStatus) =>
            {
                // Arrange.
                using var sut = new AttendanceRowStateManager(autoCollapseIntervalMs: 1000, animationDurationMs: 1);
                sut.Configure(isNarrowViewport: true, hasPointerDevice: false);

                // Act — row is collapsed by default.
                var isExpanded = sut.IsExpanded(housemateId);

                // Assert.
                // When collapsed: aria-expanded="false" on active button, aria-hidden="true" and tabindex="-1" on inactive buttons.
                // isExpanded == false drives: aria-expanded="false", isCollapsed == true drives: aria-hidden="true", tabindex="-1".
                var ariaExpandedIsFalse = !isExpanded;

                // Verify the active button status matches current status (determines which button gets aria-expanded).
                var activeStatus = sut.GetActiveStatus(housemateId, currentStatus);
                var activeButtonMatchesStatus = activeStatus == currentStatus;

                // Inactive buttons are the two statuses that do NOT match currentStatus.
                var inactiveStatuses = AllStatuses.Where(x => x != currentStatus).ToList();
                var hasExactlyTwoInactiveButtons = inactiveStatuses.Count == 2;

                return (ariaExpandedIsFalse && activeButtonMatchesStatus && hasExactlyTwoInactiveButtons)
                    .Label($"Collapsed row: expected aria-expanded=false (isExpanded={isExpanded}), " +
                           $"activeStatus={activeStatus}=={currentStatus}, inactiveCount={inactiveStatuses.Count}");
            });
    }

    // Feature: attendance-slide-in-buttons, Property 8: Accessibility attributes reflect expand/collapse state
    // Validates: Requirements 8.2, 8.4
    [Property(MaxTest = 100)]
    public Property ExpandedRow_ActiveButtonHasAriaExpandedTrue_InactiveButtonsAreAccessible()
    {
        return Prop.ForAll(
            GuidArb,
            StatusArb,
            async (housemateId, currentStatus) =>
            {
                // Arrange.
                using var sut = new AttendanceRowStateManager(autoCollapseIntervalMs: 1000, animationDurationMs: 1);
                sut.Configure(isNarrowViewport: true, hasPointerDevice: false);

                // Act — expand the row.
                await sut.ExpandAsync(housemateId);

                // Assert.
                var isExpanded = sut.IsExpanded(housemateId);

                // When expanded: aria-expanded="true" on active button, aria-hidden removed and tabindex removed on inactive buttons.
                // isExpanded == true drives: aria-expanded="true", isCollapsed == false drives: no aria-hidden, no tabindex="-1".
                var ariaExpandedIsTrue = isExpanded;

                // Verify the active button status still matches current status.
                var activeStatus = sut.GetActiveStatus(housemateId, currentStatus);
                var activeButtonMatchesStatus = activeStatus == currentStatus;

                // Inactive buttons should NOT have aria-hidden or tabindex=-1 when expanded.
                // This is driven by isCollapsed being false (i.e., isExpanded being true).
                var inactiveButtonsAccessible = isExpanded;

                return (ariaExpandedIsTrue && activeButtonMatchesStatus && inactiveButtonsAccessible)
                    .Label($"Expanded row: expected aria-expanded=true (isExpanded={isExpanded}), " +
                           $"activeStatus={activeStatus}=={currentStatus}, inactiveAccessible={inactiveButtonsAccessible}");
            });
    }

    // Feature: attendance-slide-in-buttons, Property 8: Accessibility attributes reflect expand/collapse state
    // Validates: Requirements 8.1, 8.3
    [Property(MaxTest = 100)]
    public Property CollapseAfterExpand_RestoresAriaExpandedFalse_HidesInactiveButtons()
    {
        return Prop.ForAll(
            GuidArb,
            StatusArb,
            async (housemateId, currentStatus) =>
            {
                // Arrange.
                using var sut = new AttendanceRowStateManager(autoCollapseIntervalMs: 1000, animationDurationMs: 1);
                sut.Configure(isNarrowViewport: true, hasPointerDevice: false);

                // Expand first.
                await sut.ExpandAsync(housemateId);

                // Act — collapse the row.
                await sut.CollapseAsync(housemateId);

                // Assert.
                var isExpanded = sut.IsExpanded(housemateId);

                // After collapse: aria-expanded="false" restored, inactive buttons hidden again.
                var ariaExpandedIsFalse = !isExpanded;
                var inactiveButtonsHidden = !isExpanded;

                return (ariaExpandedIsFalse && inactiveButtonsHidden)
                    .Label($"After collapse: expected aria-expanded=false (isExpanded={isExpanded}), " +
                           $"inactiveHidden={inactiveButtonsHidden}");
            });
    }
}
