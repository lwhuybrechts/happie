using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services;

namespace Happie.Web.Tests.Services;

// Feature: attendance-slide-in-buttons, Property 7: Single row expansion policy
public class AttendanceRowStateManagerSingleRowExpansionPropertyTests
{
    private static readonly Arbitrary<(Guid A, Guid B)> TwoDistinctGuidArb =
        Gen.Choose(1, int.MaxValue)
            .Select(x => new Guid(x, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0))
            .Two()
            .Where(x => x.Item1 != x.Item2)
            .Select(x => (A: x.Item1, B: x.Item2))
            .ToArbitrary();

    // Feature: attendance-slide-in-buttons, Property 7: Single row expansion policy
    // Validates: Requirements 6.2, 6.3
    [Property(MaxTest = 100)]
    public Property ExpandAsync_AnotherRowExpanded_OnlyNewRowIsExpanded()
    {
        return Prop.ForAll(
            TwoDistinctGuidArb,
            async guids =>
            {
                // Arrange.
                using var sut = new AttendanceRowStateManager(autoCollapseIntervalMs: 1000, animationDurationMs: 1);
                sut.Configure(isNarrowViewport: true, hasPointerDevice: false);
                await sut.ExpandAsync(guids.A);

                // Act.
                await sut.ExpandAsync(guids.B);

                // Assert.
                var bIsExpanded = sut.IsExpanded(guids.B);
                var aIsNotExpanded = !sut.IsExpanded(guids.A);

                return (bIsExpanded && aIsNotExpanded)
                    .Label($"A={guids.A}, B={guids.B}: expected only B expanded, got B.IsExpanded={bIsExpanded}, A.IsExpanded={!aIsNotExpanded}");
            });
    }
}
