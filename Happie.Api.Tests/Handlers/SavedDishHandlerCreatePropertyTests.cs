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

// Feature: saved-dishes, Property 3: Create enforces uniqueness and reactivates soft-deleted
/// <summary>Property-based tests for <see cref="SavedDishHandler.CreateAsync"/> uniqueness and reactivation behavior.</summary>
public class SavedDishHandlerCreatePropertyTests
{
    private readonly Mock<ISavedDishRepository> _savedDishRepositoryMock = new();
    private readonly Mock<IDishRepository> _dishRepositoryMock = new();
    private readonly SavedDishHandler _sut;

    /// <summary>Initializes a new instance of <see cref="SavedDishHandlerCreatePropertyTests"/>.</summary>
    public SavedDishHandlerCreatePropertyTests()
    {
        _sut = new SavedDishHandler(
            _savedDishRepositoryMock.Object,
            _dishRepositoryMock.Object,
            NullLogger<SavedDishHandler>.Instance);
    }

    /// <summary>
    /// For any household with a set of existing SavedDishes (active and soft-deleted),
    /// creating a new SavedDish with a description that matches (case-insensitive, trimmed)
    /// an active dish should return AlreadyExists, matching a soft-deleted dish should reactivate
    /// that record (preserving its ID and setting IsDeleted to false), and matching no existing
    /// dish should create a new record.
    /// Validates: Requirements 1.3, 1.4, 1.5, 6.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CreateAsync_EnforcesUniquenessAndReactivatesSoftDeleted()
    {
        return Prop.ForAll(
            CreateScenarioArb(),
            async scenario =>
            {
                // Arrange.
                _savedDishRepositoryMock.Reset();
                _dishRepositoryMock.Reset();

                _savedDishRepositoryMock
                    .Setup(x => x.GetAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(scenario.ExistingDishes);

                _savedDishRepositoryMock
                    .Setup(x => x.UpsertAsync(It.IsAny<SavedDish>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                _dishRepositoryMock
                    .Setup(x => x.GetAllByPartitionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<DishRecord>());

                // Act.
                var result = await _sut.CreateAsync(scenario.HouseholdId, scenario.Description);

                // Assert.
                return scenario.ExpectedOutcome switch
                {
                    ExpectedCreateOutcome.AlreadyExists =>
                        (result.Outcome == SavedDishCreateOutcome.AlreadyExists)
                            .Label($"Expected AlreadyExists but got {result.Outcome} for description '{scenario.Description}'"),

                    ExpectedCreateOutcome.Reactivated =>
                        (result.Outcome == SavedDishCreateOutcome.Reactivated)
                            .Label($"Expected Reactivated but got {result.Outcome} for description '{scenario.Description}'")
                            .And((result.SavedDish != null)
                                .Label("Reactivated result should have a non-null SavedDish"))
                            .And((result.SavedDish!.Id == scenario.ExpectedReactivatedId)
                                .Label($"Reactivated dish ID mismatch: expected {scenario.ExpectedReactivatedId} but got {result.SavedDish.Id}"))
                            .And((!result.SavedDish.IsDeleted)
                                .Label("Reactivated dish should have IsDeleted = false")),

                    ExpectedCreateOutcome.Created =>
                        (result.Outcome == SavedDishCreateOutcome.Created)
                            .Label($"Expected Created but got {result.Outcome} for description '{scenario.Description}'")
                            .And((result.SavedDish != null)
                                .Label("Created result should have a non-null SavedDish"))
                            .And((!result.SavedDish!.IsDeleted)
                                .Label("Created dish should have IsDeleted = false"))
                            .And(string.Equals(result.SavedDish.Description, scenario.Description.Trim(), StringComparison.Ordinal)
                                .Label($"Created dish description mismatch: expected '{scenario.Description.Trim()}' but got '{result.SavedDish.Description}'")),

                    _ => throw new InvalidOperationException($"Unhandled {nameof(ExpectedCreateOutcome)}: {scenario.ExpectedOutcome}")
                };
            });
    }

    private static Arbitrary<CreateScenario> CreateScenarioArb()
    {
        var householdIdGen = ArbMap.Default.GeneratorFor<Guid>();

        // Generate a list of 0–5 existing dishes (mix of active and soft-deleted).
        var existingDishesGen = householdIdGen.SelectMany(householdId =>
            Gen.Choose(0, 5).SelectMany(count =>
                Gen.ListOf(SavedDishGen(householdId), count)));

        // Combine household, existing dishes, and a scenario type to determine the description.
        var gen = householdIdGen.SelectMany(householdId =>
            Gen.Choose(0, 5).SelectMany(count =>
                Gen.ListOf(SavedDishGen(householdId), count).SelectMany(existingDishes =>
                    Gen.Choose(0, 2).SelectMany(scenarioType =>
                    {
                        var dishes = existingDishes.ToList();
                        var activeDishes = dishes.Where(x => !x.IsDeleted).ToList();
                        var softDeletedDishes = dishes.Where(x => x.IsDeleted).ToList();

                        return scenarioType switch
                        {
                            // Scenario 0: Match an active dish (AlreadyExists).
                            0 when activeDishes.Count > 0 =>
                                Gen.Choose(0, activeDishes.Count - 1).SelectMany(index =>
                                    CaseVariantGen(activeDishes[index].Description).Select(description =>
                                        new CreateScenario(
                                            householdId,
                                            dishes,
                                            description,
                                            ExpectedCreateOutcome.AlreadyExists,
                                            null))),

                            // Scenario 1: Match a soft-deleted dish (Reactivated).
                            1 when softDeletedDishes.Count > 0 =>
                                Gen.Choose(0, softDeletedDishes.Count - 1).SelectMany(index =>
                                    CaseVariantGen(softDeletedDishes[index].Description).Select(description =>
                                        new CreateScenario(
                                            householdId,
                                            dishes,
                                            description,
                                            ExpectedCreateOutcome.Reactivated,
                                            softDeletedDishes[index].Id))),

                            // Scenario 2 (or fallback): New description (Created).
                            _ => NewDescriptionGen(dishes).Select(description =>
                                new CreateScenario(
                                    householdId,
                                    dishes,
                                    description,
                                    ExpectedCreateOutcome.Created,
                                    null))
                        };
                    }))));

        return Arb.From(gen);
    }

    private static Gen<SavedDish> SavedDishGen(Guid householdId)
    {
        var guidGen = ArbMap.Default.GeneratorFor<Guid>();
        var printableCharGen = Gen.Choose(33, 126).Select(x => (char)x);

        var descriptionGen = Gen.Choose(1, 50)
            .SelectMany(length => Gen.ListOf(printableCharGen, length)
                .Select(chars => new string(chars.ToArray())));

        var isDeletedGen = Gen.Elements(true, false);

        return guidGen.SelectMany(id =>
            descriptionGen.SelectMany(description =>
                isDeletedGen.Select(isDeleted =>
                    new SavedDish(id, householdId, description, isDeleted))));
    }

    /// <summary>Generates a case variant of an existing description (upper, lower, or mixed with optional padding).</summary>
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

    /// <summary>Generates a description that does not match any existing dish (case-insensitive).</summary>
    private static Gen<string> NewDescriptionGen(List<SavedDish> existingDishes)
    {
        var printableCharGen = Gen.Choose(33, 126).Select(x => (char)x);

        return Gen.Choose(1, 50)
            .SelectMany(length => Gen.ListOf(printableCharGen, length)
                .Select(chars => new string(chars.ToArray())))
            .Where(description =>
                !existingDishes.Any(x =>
                    string.Equals(x.Description.Trim(), description.Trim(), StringComparison.OrdinalIgnoreCase)));
    }

    private enum ExpectedCreateOutcome
    {
        AlreadyExists,
        Reactivated,
        Created
    }

    private record CreateScenario(
        Guid HouseholdId,
        List<SavedDish> ExistingDishes,
        string Description,
        ExpectedCreateOutcome ExpectedOutcome,
        Guid? ExpectedReactivatedId);
}
