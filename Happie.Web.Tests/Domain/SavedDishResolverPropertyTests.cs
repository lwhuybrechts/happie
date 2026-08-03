using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Domain;

namespace Happie.Web.Tests.Domain;

// Feature: dish-recipes, Property 11: For any list of SavedDishIds where some IDs correspond to existing dishes and some do not, the rendered output SHALL include clickable links only for IDs that resolve to an existing saved dish.

/// <summary>Property-based tests for <see cref="SavedDishResolver"/>.</summary>
public class SavedDishResolverPropertyTests
{
    /// <summary>
    /// For any list of committed IDs containing a mix of resolvable and unresolvable IDs,
    /// the resolved output SHALL contain only IDs that exist in the available set.
    /// **Validates: Requirements 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Resolve_MixedIds_OnlyResolvableIdsAppear()
    {
        return Prop.ForAll(
            AvailableIdsArb(),
            (availableIds) => Prop.ForAll(
                CommittedIdsArb(availableIds),
                (committedIds) =>
                {
                    // Arrange.
                    var availableSet = availableIds.ToHashSet();

                    // Act.
                    var result = SavedDishResolver.Resolve(committedIds, availableSet);

                    // Assert.
                    return result.All(x => availableSet.Contains(x))
                        .Label($"All resolved IDs must exist in available set. Result: [{string.Join(", ", result)}], Available: [{string.Join(", ", availableSet)}]");
                }));
    }

    /// <summary>
    /// For any list of committed IDs, unresolvable IDs (those not in the available set)
    /// SHALL NOT appear in the resolved output.
    /// **Validates: Requirements 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Resolve_UnresolvableIds_AreOmitted()
    {
        return Prop.ForAll(
            AvailableIdsArb(),
            (availableIds) => Prop.ForAll(
                CommittedIdsArb(availableIds),
                (committedIds) =>
                {
                    // Arrange.
                    var availableSet = availableIds.ToHashSet();
                    var unresolvableIds = committedIds.Where(x => !availableSet.Contains(x)).ToList();

                    // Act.
                    var result = SavedDishResolver.Resolve(committedIds, availableSet);

                    // Assert.
                    return unresolvableIds.All(x => !result.Contains(x))
                        .Label($"Unresolvable IDs must not appear in result. Unresolvable: [{string.Join(", ", unresolvableIds)}], Result: [{string.Join(", ", result)}]");
                }));
    }

    /// <summary>
    /// For any list of committed IDs, all resolvable IDs (those present in the available set)
    /// SHALL appear in the resolved output.
    /// **Validates: Requirements 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Resolve_ResolvableIds_AreIncluded()
    {
        return Prop.ForAll(
            AvailableIdsArb(),
            (availableIds) => Prop.ForAll(
                CommittedIdsArb(availableIds),
                (committedIds) =>
                {
                    // Arrange.
                    var availableSet = availableIds.ToHashSet();
                    var resolvableIds = committedIds.Where(x => availableSet.Contains(x)).ToList();

                    // Act.
                    var result = SavedDishResolver.Resolve(committedIds, availableSet);

                    // Assert.
                    return resolvableIds.All(x => result.Contains(x))
                        .Label($"All resolvable IDs must appear in result. Resolvable: [{string.Join(", ", resolvableIds)}], Result: [{string.Join(", ", result)}]");
                }));
    }

    /// <summary>
    /// The resolved output SHALL preserve the order of IDs from the committed list.
    /// **Validates: Requirements 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Resolve_PreservesOrder_FromCommittedList()
    {
        return Prop.ForAll(
            AvailableIdsArb(),
            (availableIds) => Prop.ForAll(
                CommittedIdsArb(availableIds),
                (committedIds) =>
                {
                    // Arrange.
                    var availableSet = availableIds.ToHashSet();
                    var expectedOrder = committedIds.Where(x => availableSet.Contains(x)).ToList();

                    // Act.
                    var result = SavedDishResolver.Resolve(committedIds, availableSet);

                    // Assert.
                    return result.SequenceEqual(expectedOrder)
                        .Label($"Result order must match committed order. Expected: [{string.Join(", ", expectedOrder)}], Got: [{string.Join(", ", result)}]");
                }));
    }

    private static Arbitrary<List<Guid>> AvailableIdsArb()
    {
        // Generate 1-10 unique available dish IDs.
        var guidGen = ArbMap.Default.GeneratorFor<Guid>();
        return Gen.Choose(1, 10)
            .SelectMany(count => guidGen.ListOf(count))
            .Select(x => x.Distinct().ToList())
            .ToArbitrary();
    }

    private static Arbitrary<List<Guid>> CommittedIdsArb(List<Guid> availableIds)
    {
        // Generate committed IDs: a mix of some from available and some random (unresolvable).
        var guidGen = ArbMap.Default.GeneratorFor<Guid>();

        var fromAvailable = Gen.Choose(0, availableIds.Count)
            .SelectMany(count => Gen.Shuffle(availableIds.ToArray()).Select(x => x.Take(count).ToList()));

        var randomIds = Gen.Choose(0, 5)
            .SelectMany(count => guidGen.ListOf(count).Select(x => x.ToList()));

        return fromAvailable
            .SelectMany(available => randomIds.Select(random => available.Concat(random).ToList()))
            .SelectMany(combined => Gen.Shuffle(combined.ToArray()).Select(x => x.ToList()))
            .ToArbitrary();
    }
}
