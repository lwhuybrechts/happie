using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Domain;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Happie.Api.Tests.Handlers;

// Feature: saved-dishes, Property 5: Suggestions are distinct, unmatched, recent, and limited to 5
/// <summary>Property-based tests for <see cref="SavedDishHandler.GetSuggestionsAsync"/>.</summary>
public class SavedDishHandlerSuggestionsPropertyTests
{
    private readonly Mock<ISavedDishRepository> _savedDishRepositoryMock = new();
    private readonly Mock<IDishRepository> _dishRepositoryMock = new();
    private readonly SavedDishHandler _sut;

    /// <summary>Initializes a new instance of <see cref="SavedDishHandlerSuggestionsPropertyTests"/>.</summary>
    public SavedDishHandlerSuggestionsPropertyTests()
    {
        _sut = new SavedDishHandler(
            _savedDishRepositoryMock.Object,
            _dishRepositoryMock.Object,
            NullLogger<SavedDishHandler>.Instance);
    }

    /// <summary>
    /// For any household with a set of DishRecords and a set of
    /// SavedDishes (active and soft-deleted), the suggestions computation should return at most 5 distinct
    /// descriptions from DishRecords where description is non-empty, and the description
    /// does not match any SavedDish (case-insensitive, trimmed), ordered by most recent date first.
    /// Validates: Requirements 5.2, 5.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetSuggestionsAsync_ReturnsDistinctUnmatchedRecentLimitedToFive()
    {
        return Prop.ForAll(
            DishRecordListArb(),
            SavedDishListArb(),
            async (dishRecords, savedDishes) =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();

                _dishRepositoryMock
                    .Setup(x => x.GetAllByPartitionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(dishRecords);

                _savedDishRepositoryMock
                    .Setup(x => x.GetAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(savedDishes);

                // Act.
                var result = await _sut.GetSuggestionsAsync(householdId);

                // Assert.
                var savedDescriptions = savedDishes
                    .Select(x => x.Description.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Candidates: non-empty description, not matching any saved dish.
                var candidates = dishRecords
                    .Where(x => !string.IsNullOrWhiteSpace(x.Description) &&
                                !savedDescriptions.Contains(x.Description.Trim()))
                    .OrderByDescending(x => x.Date)
                    .ToList();

                // Expected distinct (case-insensitive), limited to 5.
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var expectedSuggestions = new List<string>();
                foreach (var record in candidates)
                {
                    var trimmed = record.Description.Trim();
                    if (seen.Add(trimmed))
                    {
                        expectedSuggestions.Add(trimmed);
                        if (expectedSuggestions.Count >= 5)
                            break;
                    }
                }

                var atMostFive = (result.Count <= 5)
                    .Label($"Expected at most 5 suggestions but got {result.Count}");

                var allDistinct = (result.Distinct(StringComparer.OrdinalIgnoreCase).Count() == result.Count)
                    .Label("Suggestions contain duplicates (case-insensitive)");

                var noneMatchSavedDishes = result.All(x => !savedDescriptions.Contains(x.Trim()))
                    .Label("A suggestion matches a saved dish description");

                var noneFromLinkedRecords = result.All(suggestion =>
                    dishRecords.Any(x => !string.IsNullOrWhiteSpace(x.Description) &&
                                         string.Equals(x.Description.Trim(), suggestion, StringComparison.OrdinalIgnoreCase)))
                    .Label("A suggestion did not come from any DishRecord");

                var correctOrder = result.SequenceEqual(expectedSuggestions)
                    .Label($"Expected [{string.Join(", ", expectedSuggestions)}] but got [{string.Join(", ", result)}]");

                return atMostFive
                    .And(allDistinct)
                    .And(noneMatchSavedDishes)
                    .And(noneFromLinkedRecords)
                    .And(correctOrder);
            });
    }

    private static Arbitrary<List<DishRecord>> DishRecordListArb()
    {
        var gen = Gen.Choose(0, 15).SelectMany(count =>
            Gen.ListOf(DishRecordGen(), count));

        return Arb.From(gen.Select(x => x.ToList()));
    }

    private static Gen<DishRecord> DishRecordGen()
    {
        var guidGen = ArbMap.Default.GeneratorFor<Guid>();
        var printableCharGen = Gen.Choose(33, 126).Select(x => (char)x);

        // Description: mix of empty, whitespace, and actual strings.
        var descriptionGen = Gen.OneOf(
            Gen.Constant(string.Empty),
            Gen.Constant("   "),
            Gen.Choose(1, 30)
                .SelectMany(length => Gen.ListOf(
                    Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                                 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                                 'u', 'v', 'w', 'x', 'y', 'z', 'A', 'B', 'C', 'D',
                                 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N'),
                    length)
                    .Select(chars => new string(chars.ToArray()))),
            // Descriptions with leading/trailing whitespace.
            Gen.Choose(1, 15)
                .SelectMany(length => Gen.ListOf(
                    Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g'),
                    length)
                    .Select(chars => "  " + new string(chars.ToArray()) + "  ")));

        // SavedDishId removed — DishRecord no longer has this field.
        // Date: random dates within a reasonable range.
        var dateGen = Gen.Choose(0, 365)
            .Select(x => DateOnly.FromDateTime(DateTime.Today.AddDays(-x)));

        return guidGen.SelectMany(householdId =>
            dateGen.SelectMany(date =>
                descriptionGen.Select(description =>
                        new DishRecord(
                            householdId,
                            date,
                            description,
                            null,
                            null,
                            null,
                            null))));
    }

    private static Arbitrary<List<SavedDish>> SavedDishListArb()
    {
        var gen = Gen.Choose(0, 8).SelectMany(count =>
            Gen.ListOf(SavedDishGen(), count));

        return Arb.From(gen.Select(x => x.ToList()));
    }

    private static Gen<SavedDish> SavedDishGen()
    {
        var guidGen = ArbMap.Default.GeneratorFor<Guid>();

        var descriptionGen = Gen.Choose(1, 30)
            .SelectMany(length => Gen.ListOf(
                Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                             'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                             'u', 'v', 'w', 'x', 'y', 'z', 'A', 'B', 'C', 'D',
                             'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N'),
                length)
                .Select(chars => new string(chars.ToArray())));

        var isDeletedGen = Gen.Elements(true, false);

        return guidGen.SelectMany(id =>
            guidGen.SelectMany(householdId =>
                descriptionGen.SelectMany(description =>
                    isDeletedGen.Select(isDeleted =>
                        new SavedDish(id, householdId, description, isDeleted)))));
    }
}
