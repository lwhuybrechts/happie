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

// Feature: saved-dishes, Property 6: Retroactive conversion links all matching DishRecords
/// <summary>
/// For any household with DishRecords and a newly created SavedDish, after retroactive conversion,
/// every DishRecord where <c>SavedDishId</c> was null and the description matched the new SavedDish
/// (case-insensitive, trimmed) should now have <c>SavedDishId</c> set to the new SavedDish's ID
/// and description set to empty string. DishRecords that did not match should remain unchanged.
/// Validates: Requirements 7.1, 7.2
/// </summary>
public class SavedDishHandlerConversionPropertyTests
{
    [Property(MaxTest = 100)]
    public Property CreateAsync_RetroactiveConversion_LinksAllMatchingDishRecords()
    {
        return Prop.ForAll(
            ConversionScenarioArb(),
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
                    .Setup(x => x.GetAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<SavedDish>());

                savedDishRepositoryMock
                    .Setup(x => x.UpsertAsync(It.IsAny<SavedDish>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                dishRepositoryMock
                    .Setup(x => x.GetAllByPartitionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(scenario.DishRecords);

                var upsertedRecords = new List<DishRecord>();
                dishRepositoryMock
                    .Setup(x => x.UpsertAsync(It.IsAny<DishRecord>(), It.IsAny<CancellationToken>()))
                    .Callback<DishRecord, CancellationToken>((record, _) => upsertedRecords.Add(record))
                    .Returns(Task.CompletedTask);

                // Act.
                var result = await sut.CreateAsync(scenario.HouseholdId, scenario.Description);

                // Assert.
                var createdDish = result.SavedDish!;
                var trimmedDescription = scenario.Description.Trim();

                // Determine which records should have been converted.
                var expectedConverted = scenario.DishRecords
                    .Where(x => x.SavedDishId is null &&
                                string.Equals(x.Description.Trim(), trimmedDescription, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Determine which records should NOT have been converted.
                var expectedUnchanged = scenario.DishRecords
                    .Where(x => x.SavedDishId is not null ||
                                !string.Equals(x.Description.Trim(), trimmedDescription, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // All matching records should have been upserted.
                var allMatchingConverted = (upsertedRecords.Count == expectedConverted.Count)
                    .Label($"Expected {expectedConverted.Count} upserted records but got {upsertedRecords.Count}");

                // All upserted records should have SavedDishId set to the new dish's ID.
                var allHaveCorrectSavedDishId = upsertedRecords
                    .All(x => x.SavedDishId == createdDish.Id)
                    .Label("All converted records should have SavedDishId set to the new SavedDish's ID");

                // All upserted records should have description cleared.
                var allHaveEmptyDescription = upsertedRecords
                    .All(x => x.Description == string.Empty)
                    .Label("All converted records should have Description set to empty string");

                // No unchanged records should have been upserted.
                var unchangedNotUpserted = expectedUnchanged
                    .All(unchanged => !upsertedRecords.Any(upserted =>
                        upserted.HouseholdId == unchanged.HouseholdId && upserted.Date == unchanged.Date))
                    .Label("DishRecords that did not match should not have been upserted");

                return allMatchingConverted
                    .And(allHaveCorrectSavedDishId)
                    .And(allHaveEmptyDescription)
                    .And(unchangedNotUpserted);
            });
    }

    private static Arbitrary<ConversionScenario> ConversionScenarioArb()
    {
        // Use letters and digits to avoid control characters.
        var safeCharGen = Gen.Elements(
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
            'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
            'u', 'v', 'w', 'x', 'y', 'z', 'A', 'B', 'C', 'D',
            'E', 'F', 'G', 'H', 'I', 'J', '0', '1', '2', '3');

        // Generate a valid description for the new saved dish (1–50 chars).
        var descriptionGen = Gen.Choose(1, 50)
            .SelectMany(length => safeCharGen.ArrayOf(length)
                .Select(chars => new string(chars)));

        var gen = ArbMap.Default.GeneratorFor<Guid>().SelectMany(householdId =>
            descriptionGen.SelectMany(description =>
                Gen.Choose(0, 15).SelectMany(recordCount =>
                    Gen.ListOf(DishRecordGen(householdId, description, safeCharGen), recordCount)
                        .Select(records =>
                        {
                            // Ensure unique dates across generated records (DishRecords are keyed by date).
                            var uniqueRecords = records
                                .GroupBy(x => x.Date)
                                .Select(x => x.First())
                                .ToList();

                            return new ConversionScenario(
                                householdId,
                                description,
                                uniqueRecords);
                        }))));

        return Arb.From(gen);
    }

    /// <summary>
    /// Generates DishRecords for a household. Some will have matching descriptions (case-insensitive
    /// variants of the target), some will have non-matching descriptions, and some will already
    /// have a SavedDishId set.
    /// </summary>
    private static Gen<DishRecord> DishRecordGen(Guid householdId, string targetDescription, Gen<char> charGen)
    {
        // Non-matching description.
        var nonMatchingDescriptionGen = Gen.Choose(1, 50)
            .SelectMany(length => charGen.ArrayOf(length)
                .Select(chars => new string(chars)))
            .Where(x => !string.Equals(x.Trim(), targetDescription.Trim(), StringComparison.OrdinalIgnoreCase));

        // Matching description (case variants and padding).
        var matchingDescriptionGen = Gen.Choose(0, 3).Select(variant => variant switch
        {
            0 => targetDescription.ToUpperInvariant(),
            1 => targetDescription.ToLowerInvariant(),
            2 => $"  {targetDescription}  ",
            _ => targetDescription
        });

        var dateGen = Gen.Choose(0, 365).Select(x => DateOnly.FromDayNumber(738000 + x));

        // Generate one of three record types.
        return Gen.Choose(0, 2).SelectMany(recordType => recordType switch
        {
            // Matching description, no SavedDishId (should be converted).
            0 => matchingDescriptionGen.SelectMany(description =>
                dateGen.Select(date => new DishRecord(
                    householdId,
                    date,
                    description,
                    null,
                    null,
                    null,
                    null,
                    null))),

            // Non-matching description, no SavedDishId (should NOT be converted).
            1 => nonMatchingDescriptionGen.SelectMany(description =>
                dateGen.Select(date => new DishRecord(
                    householdId,
                    date,
                    description,
                    null,
                    null,
                    null,
                    null,
                    null))),

            // Has an existing SavedDishId set (should NOT be converted even if description matches).
            _ => Gen.OneOf(matchingDescriptionGen, nonMatchingDescriptionGen).SelectMany(description =>
                dateGen.SelectMany(date =>
                    ArbMap.Default.GeneratorFor<Guid>().Select(existingSavedDishId => new DishRecord(
                        householdId,
                        date,
                        description,
                        null,
                        null,
                        null,
                        null,
                        existingSavedDishId))))
        });
    }

    private record ConversionScenario(
        Guid HouseholdId,
        string Description,
        List<DishRecord> DishRecords);
}
