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
/// every DishRecord where the description matched the new SavedDish (case-insensitive, trimmed)
/// should now have its description set to empty string. DishRecords that did not match should
/// remain unchanged.
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
                var dayPlanDishLinkRepositoryMock = new Mock<IDayPlanDishLinkRepository>();
                var sut = new SavedDishHandler(
                    savedDishRepositoryMock.Object,
                    dishRepositoryMock.Object,
                    dayPlanDishLinkRepositoryMock.Object,
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

                // Return empty list — simulate no existing links for any date.
                dayPlanDishLinkRepositoryMock
                    .Setup(x => x.GetAllByHouseholdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<DayPlanDishLink>());

                var createdLinks = new List<DayPlanDishLink>();
                dayPlanDishLinkRepositoryMock
                    .Setup(x => x.CreateAsync(It.IsAny<DayPlanDishLink>(), It.IsAny<CancellationToken>()))
                    .Callback<DayPlanDishLink, CancellationToken>((link, _) => createdLinks.Add(link))
                    .Returns(Task.CompletedTask);

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
                    .Where(x => !string.IsNullOrWhiteSpace(x.Description) &&
                                string.Equals(x.Description.Trim(), trimmedDescription, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Determine which records should NOT have been converted.
                var expectedUnchanged = scenario.DishRecords
                    .Where(x => string.IsNullOrWhiteSpace(x.Description) ||
                                !string.Equals(x.Description.Trim(), trimmedDescription, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // All matching records should have links created.
                var allMatchingLinked = (createdLinks.Count == expectedConverted.Count)
                    .Label($"Expected {expectedConverted.Count} links created but got {createdLinks.Count}");

                // All created links should reference the created SavedDish with SortOrder 0.
                var allLinksCorrect = createdLinks
                    .All(x => x.SavedDishId == createdDish.Id && x.SortOrder == 0)
                    .Label("All created links should reference the created SavedDish with SortOrder 0");

                // All matching records should have been upserted with empty description.
                var allMatchingConverted = (upsertedRecords.Count == expectedConverted.Count)
                    .Label($"Expected {expectedConverted.Count} upserted records but got {upsertedRecords.Count}");

                // All upserted records should have description cleared.
                var allHaveEmptyDescription = upsertedRecords
                    .All(x => x.Description == string.Empty)
                    .Label("All converted records should have Description set to empty string");

                // No unchanged records should have been upserted.
                var unchangedNotUpserted = expectedUnchanged
                    .All(unchanged => !upsertedRecords.Any(upserted =>
                        upserted.HouseholdId == unchanged.HouseholdId && upserted.Date == unchanged.Date))
                    .Label("DishRecords that did not match should not have been upserted");

                return allMatchingLinked
                    .And(allLinksCorrect)
                    .And(allMatchingConverted)
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
    /// variants of the target) and some will have non-matching descriptions.
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

        // Generate one of two record types.
        return Gen.Choose(0, 1).SelectMany(recordType => recordType switch
        {
            // Matching description (should be converted).
            0 => matchingDescriptionGen.SelectMany(description =>
                dateGen.Select(date => new DishRecord(
                    householdId,
                    date,
                    description,
                    null,
                    null,
                    null,
                    null))),

            // Non-matching description (should NOT be converted).
            _ => nonMatchingDescriptionGen.SelectMany(description =>
                dateGen.Select(date => new DishRecord(
                    householdId,
                    date,
                    description,
                    null,
                    null,
                    null,
                    null)))
        });
    }

    private record ConversionScenario(
        Guid HouseholdId,
        string Description,
        List<DishRecord> DishRecords);

    // Feature: saved-dishes, Property 6: Retroactive conversion excludes dates with existing links
    /// <summary>
    /// For any household with DishRecords where some dates already have DayPlanDishLink entities,
    /// retroactive conversion should only create links for matching DishRecords on dates WITHOUT
    /// existing links. DishRecords on dates WITH existing links should not be converted, even if
    /// their description matches.
    /// Validates: Requirements 9.1, 9.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CreateAsync_RetroactiveConversion_ExcludesDatesWithExistingLinks()
    {
        return Prop.ForAll(
            ConversionWithExistingLinksScenarioArb(),
            async scenario =>
            {
                // Arrange.
                var savedDishRepositoryMock = new Mock<ISavedDishRepository>();
                var dishRepositoryMock = new Mock<IDishRepository>();
                var dayPlanDishLinkRepositoryMock = new Mock<IDayPlanDishLinkRepository>();
                var sut = new SavedDishHandler(
                    savedDishRepositoryMock.Object,
                    dishRepositoryMock.Object,
                    dayPlanDishLinkRepositoryMock.Object,
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

                // Return existing links — these dates should be excluded from conversion.
                dayPlanDishLinkRepositoryMock
                    .Setup(x => x.GetAllByHouseholdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(scenario.ExistingLinks);

                var createdLinks = new List<DayPlanDishLink>();
                dayPlanDishLinkRepositoryMock
                    .Setup(x => x.CreateAsync(It.IsAny<DayPlanDishLink>(), It.IsAny<CancellationToken>()))
                    .Callback<DayPlanDishLink, CancellationToken>((link, _) => createdLinks.Add(link))
                    .Returns(Task.CompletedTask);

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
                var datesWithLinks = scenario.ExistingLinks.Select(x => x.Date).ToHashSet();

                // Records that match AND are on dates WITHOUT existing links should be converted.
                var expectedConverted = scenario.DishRecords
                    .Where(x => !datesWithLinks.Contains(x.Date) &&
                                !string.IsNullOrWhiteSpace(x.Description) &&
                                string.Equals(x.Description.Trim(), trimmedDescription, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Records that match BUT are on dates WITH existing links should NOT be converted.
                var matchingButLinked = scenario.DishRecords
                    .Where(x => datesWithLinks.Contains(x.Date) &&
                                !string.IsNullOrWhiteSpace(x.Description) &&
                                string.Equals(x.Description.Trim(), trimmedDescription, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Only unlinked matching records should have links created.
                var correctLinkCount = (createdLinks.Count == expectedConverted.Count)
                    .Label($"Expected {expectedConverted.Count} links created but got {createdLinks.Count}");

                // All created links should reference the created SavedDish with SortOrder 0.
                var allLinksCorrect = createdLinks
                    .All(x => x.SavedDishId == createdDish.Id && x.SortOrder == 0)
                    .Label("All created links should reference the created SavedDish with SortOrder 0");

                // All converted records should have been upserted with empty description.
                var correctUpsertCount = (upsertedRecords.Count == expectedConverted.Count)
                    .Label($"Expected {expectedConverted.Count} upserted records but got {upsertedRecords.Count}");

                var allHaveEmptyDescription = upsertedRecords
                    .All(x => x.Description == string.Empty)
                    .Label("All converted records should have Description set to empty string");

                // Records on dates WITH existing links should NOT have been converted.
                var linkedDatesNotConverted = matchingButLinked
                    .All(excluded => !createdLinks.Any(link => link.Date == excluded.Date))
                    .Label("DishRecords on dates with existing links should not have links created");

                var linkedDatesNotUpserted = matchingButLinked
                    .All(excluded => !upsertedRecords.Any(upserted => upserted.Date == excluded.Date))
                    .Label("DishRecords on dates with existing links should not have been upserted");

                // Ensure the scenario actually has some linked dates with matching records (non-trivial test).
                var hasLinkedMatchingRecords = (matchingButLinked.Count >= 1)
                    .Label($"Scenario should have at least 1 matching record on a linked date, got {matchingButLinked.Count}");

                return correctLinkCount
                    .And(allLinksCorrect)
                    .And(correctUpsertCount)
                    .And(allHaveEmptyDescription)
                    .And(linkedDatesNotConverted)
                    .And(linkedDatesNotUpserted)
                    .And(hasLinkedMatchingRecords);
            });
    }

    private static Arbitrary<ConversionWithExistingLinksScenario> ConversionWithExistingLinksScenarioArb()
    {
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
                // Generate a pool of unique dates to distribute among records and existing links.
                Gen.Choose(10, 25).SelectMany(datePoolSize =>
                    Gen.ListOf(
                        Gen.Choose(0, 365).Select(x => DateOnly.FromDayNumber(738000 + x)),
                        datePoolSize)
                    .Select(x => x.Distinct().ToList())
                    .Where(x => x.Count >= 4)
                    .SelectMany(datePool =>
                    {
                        // Split dates: some will have existing links, some won't.
                        // At least 1 date for linked-matching, at least 1 for unlinked-matching.
                        var linkedDateCount = Math.Max(1, datePool.Count / 3);
                        var linkedDates = datePool.Take(linkedDateCount).ToList();
                        var unlinkedDates = datePool.Skip(linkedDateCount).ToList();

                        // Create existing links for the linked dates.
                        var existingLinksGen = Gen.Constant(linkedDates.Select(date =>
                            new DayPlanDishLink(householdId, date, Guid.NewGuid(), 0)).ToList());

                        // Create DishRecords: some matching on linked dates, some matching on unlinked dates, some non-matching.
                        var matchingVariantGen = Gen.Choose(0, 3).Select(variant => variant switch
                        {
                            0 => description.ToUpperInvariant(),
                            1 => description.ToLowerInvariant(),
                            2 => $"  {description}  ",
                            _ => description
                        });

                        var nonMatchingDescriptionGen = Gen.Choose(1, 50)
                            .SelectMany(length => safeCharGen.ArrayOf(length)
                                .Select(chars => new string(chars)))
                            .Where(x => !string.Equals(x.Trim(), description.Trim(), StringComparison.OrdinalIgnoreCase));

                        // Build records: at least 1 matching on a linked date, at least 1 matching on an unlinked date.
                        return existingLinksGen.SelectMany(existingLinks =>
                        {
                            // At least 1 matching record on a linked date (should NOT be converted).
                            var matchingOnLinkedGen = Gen.Choose(1, Math.Min(3, linkedDates.Count))
                                .SelectMany(count => Gen.ListOf(
                                    Gen.Choose(0, linkedDates.Count - 1).SelectMany(idx =>
                                        matchingVariantGen.Select(desc =>
                                            new DishRecord(householdId, linkedDates[idx], desc, null, null, null, null))),
                                    count));

                            // At least 1 matching record on an unlinked date (should be converted).
                            var matchingOnUnlinkedGen = Gen.Choose(1, Math.Min(3, unlinkedDates.Count))
                                .SelectMany(count => Gen.ListOf(
                                    Gen.Choose(0, unlinkedDates.Count - 1).SelectMany(idx =>
                                        matchingVariantGen.Select(desc =>
                                            new DishRecord(householdId, unlinkedDates[idx], desc, null, null, null, null))),
                                    count));

                            // Some non-matching records on various dates.
                            var nonMatchingGen = Gen.Choose(0, 5)
                                .SelectMany(count => Gen.ListOf(
                                    Gen.Choose(0, datePool.Count - 1).SelectMany(idx =>
                                        nonMatchingDescriptionGen.Select(desc =>
                                            new DishRecord(householdId, datePool[idx], desc, null, null, null, null))),
                                    count));

                            return matchingOnLinkedGen.SelectMany(matchingOnLinked =>
                                matchingOnUnlinkedGen.SelectMany(matchingOnUnlinked =>
                                    nonMatchingGen.Select(nonMatching =>
                                    {
                                        var allRecords = matchingOnLinked
                                            .Concat(matchingOnUnlinked)
                                            .Concat(nonMatching)
                                            .GroupBy(x => x.Date)
                                            .Select(x => x.First())
                                            .ToList();

                                        return new ConversionWithExistingLinksScenario(
                                            householdId,
                                            description,
                                            allRecords,
                                            existingLinks);
                                    })));
                        });
                    }))));

        return Arb.From(gen);
    }

    private record ConversionWithExistingLinksScenario(
        Guid HouseholdId,
        string Description,
        List<DishRecord> DishRecords,
        List<DayPlanDishLink> ExistingLinks);
}
