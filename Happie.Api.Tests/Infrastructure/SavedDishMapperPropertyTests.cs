using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Domain;
using Happie.Api.Infrastructure.Mappers;

namespace Happie.Api.Tests.Infrastructure;

// Feature: saved-dishes, Property 1: SavedDish entity mapper round-trip
/// <summary>Property-based tests for <see cref="SavedDishMapper"/> round-trip correctness.</summary>
public class SavedDishMapperPropertyTests
{
    private readonly SavedDishMapper _sut = new();

    /// <summary>
    /// For any valid SavedDish domain object (with any valid Guid Id, Guid HouseholdId,
    /// non-empty description 1–100 chars, and boolean IsDeleted), mapping to entity via ToEntity
    /// and back via ToModel should produce an equivalent SavedDish.
    /// Validates: Requirements 1.1, 1.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToEntity_ThenToModel_PreservesSavedDish()
    {
        return Prop.ForAll(
            SavedDishArb(),
            savedDish =>
            {
                // Arrange.
                // SavedDish is already constructed by the arbitrary.

                // Act.
                var entity = _sut.ToEntity(savedDish);
                var roundTripped = _sut.ToModel(savedDish.HouseholdId, entity);

                // Assert.
                return (roundTripped.Id == savedDish.Id)
                    .Label($"Id mismatch: expected {savedDish.Id} but got {roundTripped.Id}")
                    .And((roundTripped.HouseholdId == savedDish.HouseholdId)
                        .Label($"HouseholdId mismatch: expected {savedDish.HouseholdId} but got {roundTripped.HouseholdId}"))
                    .And((roundTripped.Description == savedDish.Description)
                        .Label($"Description mismatch: expected '{savedDish.Description}' but got '{roundTripped.Description}'"))
                    .And((roundTripped.IsDeleted == savedDish.IsDeleted)
                        .Label($"IsDeleted mismatch: expected {savedDish.IsDeleted} but got {roundTripped.IsDeleted}"));
            });
    }

    private static Arbitrary<SavedDish> SavedDishArb()
    {
        var guidGen = ArbMap.Default.GeneratorFor<Guid>();

        // Printable ASCII characters excluding control characters.
        var printableCharGen = Gen.Choose(33, 126).Select(x => (char)x);

        var descriptionGen = Gen.Choose(1, 100)
            .SelectMany(length => Gen.ListOf(printableCharGen, length)
                .Select(chars => new string(chars.ToArray())));

        var isDeletedGen = Gen.Elements(true, false);

        var gen = guidGen.SelectMany(id =>
            guidGen.SelectMany(householdId =>
                descriptionGen.SelectMany(description =>
                    isDeletedGen.Select(isDeleted =>
                        new SavedDish(id, householdId, description, isDeleted)))));

        return Arb.From(gen);
    }
}
