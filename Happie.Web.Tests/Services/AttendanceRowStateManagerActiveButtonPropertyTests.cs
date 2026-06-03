using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Domain;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

// Feature: attendance-slide-in-buttons, Property 1: Active button matches current attendance status
public class AttendanceRowStateManagerActiveButtonPropertyTests
{
    private static readonly Arbitrary<AttendanceStatus> StatusArb =
        Gen.Elements(AttendanceStatus.Unknown, AttendanceStatus.EatingIn, AttendanceStatus.NotEatingIn)
            .ToArbitrary();

    private static readonly Arbitrary<Guid> GuidArb =
        ArbMap.Default.ArbFor<Guid>();

    // Feature: attendance-slide-in-buttons, Property 1: Active button matches current attendance status
    // Validates: Requirements 1.1, 1.4
    [Property(MaxTest = 100)]
    public Property GetActiveStatus_AnyStatus_ReturnsMatchingStatus()
    {
        return Prop.ForAll(
            GuidArb,
            StatusArb,
            (housemateId, currentStatus) =>
            {
                // Arrange.
                var sut = new AttendanceRowStateManager(autoCollapseIntervalMs: 1000, animationDurationMs: 1);
                sut.Configure(isNarrowViewport: true, hasPointerDevice: false);

                // Act.
                var activeStatus = sut.GetActiveStatus(housemateId, currentStatus);

                // Assert.
                return (activeStatus == currentStatus)
                    .Label($"Expected GetActiveStatus to return {currentStatus} but got {activeStatus} for housemate {housemateId}");
            });
    }
}
