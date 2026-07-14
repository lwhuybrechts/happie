using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Domain;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Happie.Api.Tests.Handlers;

// Feature: saved-dishes, Property 7: Update rejected when description unchanged
/// <summary>
/// For any household with a SavedDish, attempting to update the SavedDish's description
/// to a value that matches (case-insensitive, trimmed) a different SavedDish's description
/// (active or soft-deleted) should return AlreadyExists. Updating to a different value that
/// does not conflict should succeed. Updating a soft-deleted target returns NotFound.
/// Validates: Requirements 11.1, 11.2
/// </summary>
public class SavedDishHandlerUpdatePropertyTests
{
    /// <summary>
    /// Updating a target SavedDish's description to match another existing dish
    /// (case-insensitive, trimmed) should return AlreadyExists.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpdateAsync_DescriptionMatchesOtherDish_ReturnsAlreadyExists()
    {
        return Prop.ForAll(
            ConflictScenarioArb(),
            async scenario =>
            {
                // Arrange.
                var savedDishRepositoryMock = new Mock<ISavedDishRepository>();
                var dishRepositoryMock = new Mock<IDishRepository>();
                var sut = new SavedDishHandler(
                    savedDishRepositoryMock.Object,
                    dishRepositoryMock.Object,
                    NullLogger<SavedDishHandler>.Instance);

                savedDishRepositoryMock
                    .Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(scenario.TargetDish);

                savedDishRepositoryMock
                    .Setup(x => x.GetAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(scenario.AllDishes);

                // Act.
                var result = await sut.UpdateAsync(
                    scenario.HouseholdId,
                    scenario.TargetDish.Id,
                    scenario.NewDescription);

                // Assert.
                return (result.Outcome == SavedDishUpdateOutcome.AlreadyExists)
                    .Label($"Expected AlreadyExists but got {result.Outcome} for description '{scenario.NewDescription}' " +
                           $"conflicting with '{scenario.ConflictingDish.Description}'");
            });
    }

    /// <summary>
    /// Updating a target SavedDish's description to a non-conflicting value should succeed.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpdateAsync_NonConflictingDescription_ReturnsUpdated()
    {
        return Prop.ForAll(
            SuccessScenarioArb(),
            async scenario =>
            {
                // Arrange.
                var savedDishRepositoryMock = new Mock<ISavedDishRepository>();
                var dishRepositoryMock = new Mock<IDishRepository>();
                var sut = new SavedDishHandler(
                    savedDishRepositoryMock.Object,
                    dishRepositoryMock.Object,
                    NullLogger<SavedDishHandler>.Instance);

                savedDishRepositoryMock
                    .Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(scenario.TargetDish);

                savedDishRepositoryMock
                    .Setup(x => x.GetAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(scenario.AllDishes);

                savedDishRepositoryMock
                    .Setup(x => x.UpsertAsync(It.IsAny<SavedDish>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                // Act.
                var result = await sut.UpdateAsync(
                    scenario.HouseholdId,
                    scenario.TargetDish.Id,
                    scenario.NewDescription);

                // Assert.
                return (result.Outcome == SavedDishUpdateOutcome.Updated)
                    .Label($"Expected Updated but got {result.Outcome} for description '{scenario.NewDescription}'")
                    .And((result.SavedDish != null)
                        .Label("Updated result should have a non-null SavedDish"))
                    .And(string.Equals(result.SavedDish!.Description, scenario.NewDescription.Trim(), StringComparison.Ordinal)
                        .Label($"Updated dish description should be trimmed: expected '{scenario.NewDescription.Trim()}' but got '{result.SavedDish.Description}'"));
            });
    }

    /// <summary>
    /// Updating a soft-deleted target SavedDish should return NotFound.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpdateAsync_TargetSoftDeleted_ReturnsNotFound()
    {
        return Prop.ForAll(
            SoftDeletedTargetScenarioArb(),
            async scenario =>
            {
                // Arrange.
                var savedDishRepositoryMock = new Mock<ISavedDishRepository>();
                var dishRepositoryMock = new Mock<IDishRepository>();
                var sut = new SavedDishHandler(
                    savedDishRepositoryMock.Object,
                    dishRepositoryMock.Object,
                    NullLogger<SavedDishHandler>.Instance);

                savedDishRepositoryMock
                    .Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(scenario.TargetDish);

                // Act.
                var result = await sut.UpdateAsync(
                    scenario.HouseholdId,
                    scenario.TargetDish.Id,
                    scenario.NewDescription);

                // Assert.
                return (result.Outcome == SavedDishUpdateOutcome.NotFound)
                    .Label($"Expected NotFound but got {result.Outcome} for soft-deleted target");
            });
    }

    /// <summary>Generates a scenario where updating a target dish conflicts with another dish.</summary>
    private static Arbitrary<ConflictScenario> ConflictScenarioArb()
    {
        var householdIdGen = ArbMap.Default.GeneratorFor<Guid>();
        var printableCharGen = Gen.Choose(33, 126).Select(x => (char)x);

        var descriptionGen = Gen.Choose(1, 50)
            .SelectMany(length => Gen.ListOf(printableCharGen, length)
                .Select(chars => new string(chars.ToArray())));

        var gen = householdIdGen.SelectMany(householdId =>
            descriptionGen.SelectMany(targetDescription =>
                descriptionGen
                    .Where(x => !string.Equals(x.Trim(), targetDescription.Trim(), StringComparison.OrdinalIgnoreCase))
                    .SelectMany(conflictingDescription =>
                        Gen.Elements(true, false).SelectMany(conflictIsDeleted =>
                            CaseVariantGen(conflictingDescription).Select(newDescription =>
                            {
                                var targetDish = new SavedDish(Guid.NewGuid(), householdId, targetDescription, false);
                                var conflictingDish = new SavedDish(Guid.NewGuid(), householdId, conflictingDescription, conflictIsDeleted);
                                var allDishes = new List<SavedDish> { targetDish, conflictingDish };

                                return new ConflictScenario(
                                    householdId,
                                    targetDish,
                                    conflictingDish,
                                    allDishes,
                                    newDescription);
                            })))));

        return Arb.From(gen);
    }

    /// <summary>Generates a scenario where updating a target dish does not conflict with any other dish.</summary>
    private static Arbitrary<SuccessScenario> SuccessScenarioArb()
    {
        var householdIdGen = ArbMap.Default.GeneratorFor<Guid>();
        var printableCharGen = Gen.Choose(33, 126).Select(x => (char)x);

        var descriptionGen = Gen.Choose(1, 50)
            .SelectMany(length => Gen.ListOf(printableCharGen, length)
                .Select(chars => new string(chars.ToArray())));

        var gen = householdIdGen.SelectMany(householdId =>
            Gen.Choose(1, 5).SelectMany(dishCount =>
                Gen.ListOf(descriptionGen, dishCount)
                    .Where(descriptions => AreAllUnique(descriptions))
                    .SelectMany(descriptions =>
                    {
                        var dishes = descriptions.Select(x =>
                            new SavedDish(Guid.NewGuid(), householdId, x, false)).ToList();

                        var targetDish = dishes[0];

                        return descriptionGen
                            .Where(newDesc =>
                                !dishes.Any(x =>
                                    x.Id != targetDish.Id &&
                                    string.Equals(x.Description.Trim(), newDesc.Trim(), StringComparison.OrdinalIgnoreCase)))
                            .Where(newDesc => newDesc.Trim().Length >= 1 && newDesc.Trim().Length <= 100)
                            .Select(newDescription => new SuccessScenario(
                                householdId,
                                targetDish,
                                dishes,
                                newDescription));
                    })));

        return Arb.From(gen);
    }

    /// <summary>Generates a scenario where the target dish is soft-deleted.</summary>
    private static Arbitrary<SoftDeletedTargetScenario> SoftDeletedTargetScenarioArb()
    {
        var householdIdGen = ArbMap.Default.GeneratorFor<Guid>();
        var printableCharGen = Gen.Choose(33, 126).Select(x => (char)x);

        var descriptionGen = Gen.Choose(1, 50)
            .SelectMany(length => Gen.ListOf(printableCharGen, length)
                .Select(chars => new string(chars.ToArray())));

        var gen = householdIdGen.SelectMany(householdId =>
            descriptionGen.SelectMany(targetDescription =>
                descriptionGen.Select(newDescription =>
                {
                    var targetDish = new SavedDish(Guid.NewGuid(), householdId, targetDescription, true);
                    return new SoftDeletedTargetScenario(householdId, targetDish, newDescription);
                })));

        return Arb.From(gen);
    }

    /// <summary>Generates a case variant of an existing description (upper, lower, or with padding).</summary>
    private static Gen<string> CaseVariantGen(string original)
    {
        return Gen.Choose(0, 3).Select(variant => variant switch
        {
            0 => original.ToUpperInvariant(),
            1 => original.ToLowerInvariant(),
            2 => $"  {original}  ",
            _ => original
        });
    }

    private static bool AreAllUnique(IReadOnlyList<string> descriptions)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return descriptions.All(x => seen.Add(x.Trim()));
    }

    private record ConflictScenario(
        Guid HouseholdId,
        SavedDish TargetDish,
        SavedDish ConflictingDish,
        List<SavedDish> AllDishes,
        string NewDescription);

    private record SuccessScenario(
        Guid HouseholdId,
        SavedDish TargetDish,
        List<SavedDish> AllDishes,
        string NewDescription);

    private record SoftDeletedTargetScenario(
        Guid HouseholdId,
        SavedDish TargetDish,
        string NewDescription);
}
