using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Domain;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Results;
using Moq;

namespace Happie.Api.Tests.Handlers;

// Feature: happie, Property 5: Auto_Match links matching saved dish and reactivates if soft-deleted
/// <summary>
/// Property-based tests for <see cref="DayHandler.UpsertDishAsync"/> Auto_Match behavior.
/// When a custom-mode save's trimmed description (case-insensitive) matches an existing SavedDish,
/// the system creates a DayPlanDishLink with the matched SavedDishId and reactivates soft-deleted matches.
/// Validates: Requirements 16.1, 16.2, 16.3, 16.4
/// </summary>
public class DayHandlerAutoMatchPropertyTests
{
    private readonly Mock<IHousemateRepository> _housemateRepositoryMock = new();
    private readonly Mock<IAttendanceRepository> _attendanceRepositoryMock = new();
    private readonly Mock<IDishRepository> _dishRepositoryMock = new();
    private readonly Mock<ICommentRepository> _commentRepositoryMock = new();
    private readonly Mock<IDayHistoryRepository> _dayHistoryRepositoryMock = new();
    private readonly Mock<IPushHandler> _pushHandlerMock = new();
    private readonly Mock<ISavedDishRepository> _savedDishRepositoryMock = new();
    private readonly Mock<IDayPlanDishLinkRepository> _dayPlanDishLinkRepositoryMock = new();
    private readonly DayHandler _sut;

    /// <summary>Initializes a new instance of <see cref="DayHandlerAutoMatchPropertyTests"/>.</summary>
    public DayHandlerAutoMatchPropertyTests()
    {
        _sut = new DayHandler(
            _housemateRepositoryMock.Object,
            _attendanceRepositoryMock.Object,
            _dishRepositoryMock.Object,
            _commentRepositoryMock.Object,
            _dayHistoryRepositoryMock.Object,
            _pushHandlerMock.Object,
            _savedDishRepositoryMock.Object,
            _dayPlanDishLinkRepositoryMock.Object);
    }

    /// <summary>
    /// When a custom-mode save's trimmed description (case-insensitive) matches an active SavedDish,
    /// a DayPlanDishLink is created with the matched SavedDishId and SortOrder 0,
    /// the DishRecord description is set to empty string, and the result is Success.
    /// Validates: Requirements 16.1, 16.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_SingleActiveMatch_CreatesLinkAndClearsDescription()
    {
        return Prop.ForAll(
            SingleActiveMatchScenarioArb(),
            async scenario =>
            {
                // Reset mocks.
                ResetAllMocks();
                SetupDefaultMocks(scenario.SavedDishes);

                var capturedLinks = new List<IReadOnlyList<DayPlanDishLink>>();
                _dayPlanDishLinkRepositoryMock
                    .Setup(x => x.ReplaceAllAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyList<DayPlanDishLink>>(), It.IsAny<CancellationToken>()))
                    .Callback<Guid, DateOnly, IReadOnlyList<DayPlanDishLink>, CancellationToken>((_, _, links, _) => capturedLinks.Add(links))
                    .Returns(Task.CompletedTask);

                var capturedDishRecords = new List<DishRecord>();
                _dishRepositoryMock
                    .Setup(x => x.UpsertAsync(It.IsAny<DishRecord>(), It.IsAny<CancellationToken>()))
                    .Callback<DishRecord, CancellationToken>((record, _) => capturedDishRecords.Add(record))
                    .Returns(Task.CompletedTask);

                // Act.
                var result = await _sut.UpsertDishAsync(
                    scenario.HouseholdId,
                    scenario.Date,
                    scenario.InputDescription,
                    null,
                    null,
                    0,
                    scenario.ActingHousemateId);

                // Assert.
                var resultIsSuccess = (result == DishUpsertResult.Success)
                    .Label($"Expected Success but got {result}");

                var oneReplaceAllCall = (capturedLinks.Count == 1)
                    .Label($"Expected 1 ReplaceAllAsync call but got {capturedLinks.Count}");

                var singleLinkCreated = oneReplaceAllCall.And(
                    (capturedLinks.Count == 1 && capturedLinks[0].Count == 1)
                        .Label($"Expected 1 link but got {(capturedLinks.Count == 1 ? capturedLinks[0].Count : 0)}"));

                var linkHasCorrectSavedDishId = (capturedLinks.Count == 1 && capturedLinks[0].Count == 1 &&
                    capturedLinks[0][0].SavedDishId == scenario.ExpectedMatchedDishId)
                    .Label("Link should reference the matched SavedDishId");

                var linkHasSortOrderZero = (capturedLinks.Count == 1 && capturedLinks[0].Count == 1 &&
                    capturedLinks[0][0].SortOrder == 0)
                    .Label("Link SortOrder should be 0");

                var dishRecordHasEmptyDescription = capturedDishRecords
                    .Any(x => x.Description == string.Empty)
                    .Label("DishRecord description should be set to empty string");

                return resultIsSuccess
                    .And(singleLinkCreated)
                    .And(linkHasCorrectSavedDishId)
                    .And(linkHasSortOrderZero)
                    .And(dishRecordHasEmptyDescription);
            });
    }

    /// <summary>
    /// When the matched SavedDish is soft-deleted, it is reactivated (IsDeleted set to false)
    /// before creating the link.
    /// Validates: Requirements 16.2, 16.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_SoftDeletedMatch_ReactivatesAndCreatesLink()
    {
        return Prop.ForAll(
            SoftDeletedMatchScenarioArb(),
            async scenario =>
            {
                // Reset mocks.
                ResetAllMocks();
                SetupDefaultMocks(scenario.SavedDishes);

                var reactivatedDishes = new List<SavedDish>();
                _savedDishRepositoryMock
                    .Setup(x => x.UpsertAsync(It.IsAny<SavedDish>(), It.IsAny<CancellationToken>()))
                    .Callback<SavedDish, CancellationToken>((dish, _) => reactivatedDishes.Add(dish))
                    .Returns(Task.CompletedTask);

                var capturedLinks = new List<IReadOnlyList<DayPlanDishLink>>();
                _dayPlanDishLinkRepositoryMock
                    .Setup(x => x.ReplaceAllAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyList<DayPlanDishLink>>(), It.IsAny<CancellationToken>()))
                    .Callback<Guid, DateOnly, IReadOnlyList<DayPlanDishLink>, CancellationToken>((_, _, links, _) => capturedLinks.Add(links))
                    .Returns(Task.CompletedTask);

                _dishRepositoryMock
                    .Setup(x => x.UpsertAsync(It.IsAny<DishRecord>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                // Act.
                var result = await _sut.UpsertDishAsync(
                    scenario.HouseholdId,
                    scenario.Date,
                    scenario.InputDescription,
                    null,
                    null,
                    0,
                    scenario.ActingHousemateId);

                // Assert.
                var resultIsSuccess = (result == DishUpsertResult.Success)
                    .Label($"Expected Success but got {result}");

                var reactivationCalled = (reactivatedDishes.Count >= 1)
                    .Label($"Expected at least 1 UpsertAsync call on SavedDishRepository but got {reactivatedDishes.Count}");

                var reactivatedWithIsDeletedFalse = reactivatedDishes
                    .Any(x => x.Id == scenario.ExpectedMatchedDishId && !x.IsDeleted)
                    .Label("Matched soft-deleted SavedDish should be reactivated with IsDeleted = false");

                var linkCreated = (capturedLinks.Count == 1 && capturedLinks[0].Count >= 1 &&
                    capturedLinks[0].Any(x => x.SavedDishId == scenario.ExpectedMatchedDishId && x.SortOrder == 0))
                    .Label("Link should be created for the reactivated SavedDish with SortOrder 0");

                return resultIsSuccess
                    .And(reactivationCalled)
                    .And(reactivatedWithIsDeletedFalse)
                    .And(linkCreated);
            });
    }

    /// <summary>
    /// When the description contains " &amp; " separating multiple segments, each matching a saved dish,
    /// links are created for ALL matching dishes with correct SortOrder (0, 1, 2...).
    /// Validates: Requirements 16.1, 16.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_MultiSegmentMatch_CreatesLinksWithCorrectSortOrder()
    {
        return Prop.ForAll(
            MultiSegmentMatchScenarioArb(),
            async scenario =>
            {
                // Reset mocks.
                ResetAllMocks();
                SetupDefaultMocks(scenario.SavedDishes);

                var capturedLinks = new List<IReadOnlyList<DayPlanDishLink>>();
                _dayPlanDishLinkRepositoryMock
                    .Setup(x => x.ReplaceAllAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyList<DayPlanDishLink>>(), It.IsAny<CancellationToken>()))
                    .Callback<Guid, DateOnly, IReadOnlyList<DayPlanDishLink>, CancellationToken>((_, _, links, _) => capturedLinks.Add(links))
                    .Returns(Task.CompletedTask);

                var capturedDishRecords = new List<DishRecord>();
                _dishRepositoryMock
                    .Setup(x => x.UpsertAsync(It.IsAny<DishRecord>(), It.IsAny<CancellationToken>()))
                    .Callback<DishRecord, CancellationToken>((record, _) => capturedDishRecords.Add(record))
                    .Returns(Task.CompletedTask);

                // Act.
                var result = await _sut.UpsertDishAsync(
                    scenario.HouseholdId,
                    scenario.Date,
                    scenario.InputDescription,
                    null,
                    null,
                    0,
                    scenario.ActingHousemateId);

                // Assert.
                var resultIsSuccess = (result == DishUpsertResult.Success)
                    .Label($"Expected Success but got {result}");

                var correctLinkCount = (capturedLinks.Count == 1 &&
                    capturedLinks[0].Count == scenario.ExpectedMatchedDishIds.Count)
                    .Label($"Expected {scenario.ExpectedMatchedDishIds.Count} links but got {(capturedLinks.Count == 1 ? capturedLinks[0].Count : 0)}");

                var correctSortOrders = capturedLinks.Count == 1 &&
                    scenario.ExpectedMatchedDishIds.Select((id, index) =>
                        capturedLinks[0].Any(x => x.SavedDishId == id && x.SortOrder == index))
                    .All(x => x);
                var sortOrderLabel = correctSortOrders
                    .Label("Links should have correct SavedDishIds and SortOrder (0, 1, 2...)");

                var dishRecordHasEmptyDescription = capturedDishRecords
                    .Any(x => x.Description == string.Empty)
                    .Label("DishRecord description should be set to empty string");

                return resultIsSuccess
                    .And(correctLinkCount)
                    .And(sortOrderLabel)
                    .And(dishRecordHasEmptyDescription);
            });
    }

    /// <summary>
    /// When the description has leading/trailing spaces and different casing,
    /// the Auto_Match should still find the matching SavedDish.
    /// Validates: Requirements 16.1, 16.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_CaseInsensitiveAndTrimmedMatching_StillMatches()
    {
        return Prop.ForAll(
            CaseInsensitiveTrimmedScenarioArb(),
            async scenario =>
            {
                // Reset mocks.
                ResetAllMocks();
                SetupDefaultMocks(scenario.SavedDishes);

                var capturedLinks = new List<IReadOnlyList<DayPlanDishLink>>();
                _dayPlanDishLinkRepositoryMock
                    .Setup(x => x.ReplaceAllAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyList<DayPlanDishLink>>(), It.IsAny<CancellationToken>()))
                    .Callback<Guid, DateOnly, IReadOnlyList<DayPlanDishLink>, CancellationToken>((_, _, links, _) => capturedLinks.Add(links))
                    .Returns(Task.CompletedTask);

                _dishRepositoryMock
                    .Setup(x => x.UpsertAsync(It.IsAny<DishRecord>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                // Act.
                var result = await _sut.UpsertDishAsync(
                    scenario.HouseholdId,
                    scenario.Date,
                    scenario.InputDescription,
                    null,
                    null,
                    0,
                    scenario.ActingHousemateId);

                // Assert.
                var resultIsSuccess = (result == DishUpsertResult.Success)
                    .Label($"Expected Success but got {result}");

                var linkCreatedForMatch = (capturedLinks.Count == 1 && capturedLinks[0].Count == 1 &&
                    capturedLinks[0][0].SavedDishId == scenario.ExpectedMatchedDishId)
                    .Label("Should match despite different casing/whitespace");

                return resultIsSuccess.And(linkCreatedForMatch);
            });
    }

    private void ResetAllMocks()
    {
        _housemateRepositoryMock.Reset();
        _attendanceRepositoryMock.Reset();
        _dishRepositoryMock.Reset();
        _commentRepositoryMock.Reset();
        _dayHistoryRepositoryMock.Reset();
        _pushHandlerMock.Reset();
        _savedDishRepositoryMock.Reset();
        _dayPlanDishLinkRepositoryMock.Reset();
    }

    private void SetupDefaultMocks(IReadOnlyList<SavedDish> savedDishes)
    {
        _savedDishRepositoryMock
            .Setup(x => x.GetAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedDishes);

        _dishRepositoryMock
            .Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DishRecord?)null);

        _dayHistoryRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<DayHistoryEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _pushHandlerMock
            .Setup(x => x.SendAutoNotificationsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static Arbitrary<SingleActiveMatchScenario> SingleActiveMatchScenarioArb()
    {
        var safeCharGen = Gen.Elements(
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
            'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't');

        var descriptionGen = Gen.Choose(3, 30)
            .SelectMany(length => safeCharGen.ArrayOf(length)
                .Select(chars => new string(chars)));

        var dateGen = Gen.Choose(0, 365).Select(x => DateOnly.FromDayNumber(738000 + x));

        var gen = ArbMap.Default.GeneratorFor<Guid>().SelectMany(householdId =>
            ArbMap.Default.GeneratorFor<Guid>().SelectMany(actingHousemateId =>
                ArbMap.Default.GeneratorFor<Guid>().SelectMany(savedDishId =>
                    descriptionGen.SelectMany(description =>
                        dateGen.SelectMany(date =>
                            Gen.Choose(0, 2).Select(casingVariant =>
                            {
                                // Create an active saved dish.
                                var savedDish = new SavedDish(savedDishId, householdId, description, false);

                                // Create input description with case/whitespace variations.
                                var inputDescription = casingVariant switch
                                {
                                    0 => description.ToUpperInvariant(),
                                    1 => $"  {description}  ",
                                    _ => description.ToLowerInvariant()
                                };

                                return new SingleActiveMatchScenario(
                                    householdId,
                                    date,
                                    inputDescription,
                                    actingHousemateId,
                                    new List<SavedDish> { savedDish },
                                    savedDishId);
                            }))))));

        return Arb.From(gen);
    }

    private static Arbitrary<SoftDeletedMatchScenario> SoftDeletedMatchScenarioArb()
    {
        var safeCharGen = Gen.Elements(
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
            'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't');

        var descriptionGen = Gen.Choose(3, 30)
            .SelectMany(length => safeCharGen.ArrayOf(length)
                .Select(chars => new string(chars)));

        var dateGen = Gen.Choose(0, 365).Select(x => DateOnly.FromDayNumber(738000 + x));

        var gen = ArbMap.Default.GeneratorFor<Guid>().SelectMany(householdId =>
            ArbMap.Default.GeneratorFor<Guid>().SelectMany(actingHousemateId =>
                ArbMap.Default.GeneratorFor<Guid>().SelectMany(savedDishId =>
                    descriptionGen.SelectMany(description =>
                        dateGen.Select(date =>
                        {
                            // Create a soft-deleted saved dish.
                            var savedDish = new SavedDish(savedDishId, householdId, description, true);

                            return new SoftDeletedMatchScenario(
                                householdId,
                                date,
                                description,
                                actingHousemateId,
                                new List<SavedDish> { savedDish },
                                savedDishId);
                        })))));

        return Arb.From(gen);
    }

    private static Arbitrary<MultiSegmentMatchScenario> MultiSegmentMatchScenarioArb()
    {
        var safeCharGen = Gen.Elements(
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
            'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't');

        // Generate unique dish descriptions (no " & " inside a single description).
        var descriptionGen = Gen.Choose(3, 15)
            .SelectMany(length => safeCharGen.ArrayOf(length)
                .Select(chars => new string(chars)));

        var dateGen = Gen.Choose(0, 365).Select(x => DateOnly.FromDayNumber(738000 + x));

        // Generate 2–3 dishes.
        var gen = ArbMap.Default.GeneratorFor<Guid>().SelectMany(householdId =>
            ArbMap.Default.GeneratorFor<Guid>().SelectMany(actingHousemateId =>
                Gen.Choose(2, 3).SelectMany(dishCount =>
                    Gen.ListOf(
                        ArbMap.Default.GeneratorFor<Guid>().SelectMany(id =>
                            descriptionGen.Select(desc => (id, desc))),
                        dishCount)
                    .SelectMany(dishes =>
                        dateGen.Select(date =>
                        {
                            // Ensure unique descriptions.
                            var uniqueDishes = dishes
                                .GroupBy(x => x.desc, StringComparer.OrdinalIgnoreCase)
                                .Select(x => x.First())
                                .ToList();

                            if (uniqueDishes.Count < 2)
                                uniqueDishes = dishes.Take(2).ToList();

                            var savedDishes = uniqueDishes
                                .Select(x => new SavedDish(x.id, householdId, x.desc, false))
                                .ToList();

                            // Build " & "-joined description.
                            var inputDescription = string.Join(" & ", savedDishes.Select(x => x.Description));
                            var expectedIds = savedDishes.Select(x => x.Id).ToList();

                            return new MultiSegmentMatchScenario(
                                householdId,
                                date,
                                inputDescription,
                                actingHousemateId,
                                savedDishes.Cast<SavedDish>().ToList(),
                                expectedIds);
                        })))));

        return Arb.From(gen);
    }

    private static Arbitrary<CaseInsensitiveTrimmedScenario> CaseInsensitiveTrimmedScenarioArb()
    {
        var safeCharGen = Gen.Elements(
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
            'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't');

        var descriptionGen = Gen.Choose(3, 30)
            .SelectMany(length => safeCharGen.ArrayOf(length)
                .Select(chars => new string(chars)));

        var dateGen = Gen.Choose(0, 365).Select(x => DateOnly.FromDayNumber(738000 + x));

        // Generate leading/trailing whitespace amounts.
        var leadingSpacesGen = Gen.Choose(1, 5).Select(x => new string(' ', x));
        var trailingSpacesGen = Gen.Choose(1, 5).Select(x => new string(' ', x));

        var gen = ArbMap.Default.GeneratorFor<Guid>().SelectMany(householdId =>
            ArbMap.Default.GeneratorFor<Guid>().SelectMany(actingHousemateId =>
                ArbMap.Default.GeneratorFor<Guid>().SelectMany(savedDishId =>
                    descriptionGen.SelectMany(description =>
                        dateGen.SelectMany(date =>
                            leadingSpacesGen.SelectMany(leading =>
                                trailingSpacesGen.SelectMany(trailing =>
                                    Gen.Choose(0, 2).Select(casingVariant =>
                                    {
                                        var savedDish = new SavedDish(savedDishId, householdId, description, false);

                                        // Apply casing variant and whitespace padding.
                                        var cased = casingVariant switch
                                        {
                                            0 => description.ToUpperInvariant(),
                                            1 => description.ToLowerInvariant(),
                                            _ => MixCase(description)
                                        };
                                        var inputDescription = $"{leading}{cased}{trailing}";

                                        return new CaseInsensitiveTrimmedScenario(
                                            householdId,
                                            date,
                                            inputDescription,
                                            actingHousemateId,
                                            new List<SavedDish> { savedDish },
                                            savedDishId);
                                    }))))))));

        return Arb.From(gen);
    }

    private static string MixCase(string input)
    {
        var chars = input.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            chars[i] = i % 2 == 0 ? char.ToUpperInvariant(chars[i]) : char.ToLowerInvariant(chars[i]);
        return new string(chars);
    }

    private record SingleActiveMatchScenario(
        Guid HouseholdId,
        DateOnly Date,
        string InputDescription,
        Guid ActingHousemateId,
        IReadOnlyList<SavedDish> SavedDishes,
        Guid ExpectedMatchedDishId);

    private record SoftDeletedMatchScenario(
        Guid HouseholdId,
        DateOnly Date,
        string InputDescription,
        Guid ActingHousemateId,
        IReadOnlyList<SavedDish> SavedDishes,
        Guid ExpectedMatchedDishId);

    private record MultiSegmentMatchScenario(
        Guid HouseholdId,
        DateOnly Date,
        string InputDescription,
        Guid ActingHousemateId,
        IReadOnlyList<SavedDish> SavedDishes,
        IReadOnlyList<Guid> ExpectedMatchedDishIds);

    private record CaseInsensitiveTrimmedScenario(
        Guid HouseholdId,
        DateOnly Date,
        string InputDescription,
        Guid ActingHousemateId,
        IReadOnlyList<SavedDish> SavedDishes,
        Guid ExpectedMatchedDishId);
}
