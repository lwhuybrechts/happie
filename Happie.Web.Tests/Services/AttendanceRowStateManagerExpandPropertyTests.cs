using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services;
using Happie.Web.Tests.Helpers;

namespace Happie.Web.Tests.Services;

// Feature: attendance-slide-in-buttons, Property 2: Expand on active button click when collapsed
public class AttendanceRowStateManagerExpandPropertyTests
{
    private static readonly Arbitrary<Guid> GuidArb =
        Gen.Choose(1, int.MaxValue)
            .Select(x => new Guid(x, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0))
            .ToArbitrary();

    // Feature: attendance-slide-in-buttons, Property 2: Expand on active button click when collapsed
    // Validates: Requirements 2.1
    [Property(MaxTest = 100)]
    public Property HandleActiveButtonClickAsync_CollapsedRow_ExpandsRow()
    {
        return Prop.ForAll(
            GuidArb,
            async housemateId =>
            {
                // Arrange.
                using var sut = new AttendanceRowStateManager(new FakeDelayService());
                sut.Configure(isNarrowViewport: true, hasPointerDevice: false);

                // Act.
                await sut.HandleActiveButtonClickAsync(housemateId);

                // Assert.
                var isExpanded = sut.IsExpanded(housemateId);

                return isExpanded
                    .Label($"Expected IsExpanded({housemateId})=true after clicking active button in collapsed state, got false");
            });
    }
}
