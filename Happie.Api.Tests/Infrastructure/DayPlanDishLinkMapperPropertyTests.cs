using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Domain;
using Happie.Api.Infrastructure.Mappers;

namespace Happie.Api.Tests.Infrastructure;

// Feature: dayplan-dishlink-repartition, Property 1: Mapper round-trip preserves all fields
/// <summary>Property-based tests for <see cref="DayPlanDishLinkMapper"/> round-trip correctness.</summary>
public class DayPlanDishLinkMapperPropertyTests
{
    private readonly DayPlanDishLinkMapper _sut = new();

    /// <summary>
    /// For any valid DayPlanDishLink domain record (non-empty HouseholdId, non-empty SavedDishId,
    /// any DateOnly value within 2020–2030, and any non-negative SortOrder 0–999), mapping to
    /// entity via ToEntity and back to domain model via ToModel SHALL produce a record where all
    /// four fields (HouseholdId, Date, SavedDishId, SortOrder) are equal to the original.
    /// Validates: Requirements 1.1, 1.2, 1.3, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 5.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToEntity_ThenToModel_PreservesAllFields()
    {
        return Prop.ForAll(
            DayPlanDishLinkArb(),
            link =>
            {
                // Act.
                var entity = _sut.ToEntity(link);
                var roundTripped = _sut.ToModel(entity);

                // Assert.
                return (roundTripped.HouseholdId == link.HouseholdId)
                    .Label($"HouseholdId mismatch: expected {link.HouseholdId} but got {roundTripped.HouseholdId}")
                    .And((roundTripped.Date == link.Date)
                        .Label($"Date mismatch: expected {link.Date} but got {roundTripped.Date}"))
                    .And((roundTripped.SavedDishId == link.SavedDishId)
                        .Label($"SavedDishId mismatch: expected {link.SavedDishId} but got {roundTripped.SavedDishId}"))
                    .And((roundTripped.SortOrder == link.SortOrder)
                        .Label($"SortOrder mismatch: expected {link.SortOrder} but got {roundTripped.SortOrder}"));
            });
    }

    private static Arbitrary<DayPlanDishLink> DayPlanDishLinkArb()
    {
        var guidGen = ArbMap.Default.GeneratorFor<Guid>();

        // Generate random DateOnly within 2020-01-01 to 2030-12-31.
        var startDay = new DateOnly(2020, 1, 1).DayNumber;
        var endDay = new DateOnly(2030, 12, 31).DayNumber;
        var dateGen = Gen.Choose(startDay, endDay)
            .Select(x => DateOnly.FromDayNumber(x));

        // Generate random non-negative SortOrder (0–999).
        var sortOrderGen = Gen.Choose(0, 999);

        var gen = guidGen.SelectMany(householdId =>
            guidGen.SelectMany(savedDishId =>
                dateGen.SelectMany(date =>
                    sortOrderGen.Select(sortOrder =>
                        new DayPlanDishLink(householdId, date, savedDishId, sortOrder)))));

        return Arb.From(gen);
    }
}
