using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Domain;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Shared.Domain;
using Moq;

namespace Happie.Api.Tests.Handlers;

// Feature: happie, Property 2: Combined description resolution
/// <summary>Property-based tests for <see cref="DayHandler.GetDayPlanAsync"/> combined description resolution behavior.</summary>
public class DayHandlerCombinedDescriptionPropertyTests
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

    /// <summary>Initializes a new instance of <see cref="DayHandlerCombinedDescriptionPropertyTests"/>.</summary>
    public DayHandlerCombinedDescriptionPropertyTests()
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
    /// When DayPlanDishLinks exist for a date, the resolved description equals the descriptions of
    /// existing SavedDishes joined with " &amp; " in SortOrder, excluding links whose SavedDishId
    /// does not exist in the household. The response savedDishIds contains only existing IDs in SortOrder.
    /// Validates: Requirements 1.5, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetDayPlanAsync_LinksExist_ResolvesCombinedDescriptionFromSavedDishes()
    {
        return Prop.ForAll(
            LinksWithSavedDishesScenarioArb(),
            async scenario =>
            {
                // Arrange.
                ResetAllMocks();
                SetupCommonRepositories(scenario.HouseholdId, scenario.Date);
                SetupDishRepository(scenario.HouseholdId, scenario.Date, scenario.DishRecord);
                SetupDayPlanDishLinkRepository(scenario.HouseholdId, scenario.Date, scenario.Links);
                SetupSavedDishRepository(scenario.HouseholdId, scenario.SavedDishes);

                // Act.
                var result = await _sut.GetDayPlanAsync(scenario.HouseholdId, scenario.Date);

                // Assert — description is resolved from linked saved dishes joined with " & ".
                var savedDishById = scenario.SavedDishes.ToDictionary(x => x.Id);
                var expectedDescriptions = new List<string>();
                var expectedIds = new List<Guid>();

                foreach (var link in scenario.Links.OrderBy(x => x.SortOrder))
                {
                    if (savedDishById.TryGetValue(link.SavedDishId, out var savedDish))
                    {
                        expectedDescriptions.Add(savedDish.Description);
                        expectedIds.Add(link.SavedDishId);
                    }
                }

                var expectedDescription = string.Join(" & ", expectedDescriptions);

                var descriptionMatches = result.Dish!.Description == expectedDescription;
                var idsMatch = result.Dish.SavedDishIds != null &&
                    result.Dish.SavedDishIds.SequenceEqual(expectedIds);

                return (descriptionMatches && idsMatch)
                    .Label($"Expected description '{expectedDescription}' but got '{result.Dish.Description}'. " +
                           $"Expected IDs [{string.Join(", ", expectedIds)}] but got [{string.Join(", ", result.Dish.SavedDishIds ?? new List<Guid>())}]");
            });
    }

    /// <summary>
    /// When links exist, any non-empty DishRecord description is ignored (Combined_Description from links is used).
    /// Validates: Requirements 1.5, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetDayPlanAsync_LinksExistWithNonEmptyDishDescription_IgnoresDishRecordDescription()
    {
        return Prop.ForAll(
            LinksWithNonEmptyDishDescriptionScenarioArb(),
            async scenario =>
            {
                // Arrange.
                ResetAllMocks();
                SetupCommonRepositories(scenario.HouseholdId, scenario.Date);
                SetupDishRepository(scenario.HouseholdId, scenario.Date, scenario.DishRecord);
                SetupDayPlanDishLinkRepository(scenario.HouseholdId, scenario.Date, scenario.Links);
                SetupSavedDishRepository(scenario.HouseholdId, scenario.SavedDishes);

                // Act.
                var result = await _sut.GetDayPlanAsync(scenario.HouseholdId, scenario.Date);

                // Assert — DishRecord description is ignored; combined from saved dishes is used.
                var expectedDescription = string.Join(" & ",
                    scenario.Links.OrderBy(x => x.SortOrder)
                        .Select(x => scenario.SavedDishes.First(sd => sd.Id == x.SavedDishId).Description));

                var descriptionIgnored = result.Dish!.Description == expectedDescription;
                var dishDescriptionNotUsed = result.Dish.Description != scenario.DishRecord!.Description;

                return (descriptionIgnored && dishDescriptionNotUsed)
                    .Label($"Expected combined description '{expectedDescription}' but got '{result.Dish.Description}'. " +
                           $"DishRecord description '{scenario.DishRecord.Description}' should be ignored.");
            });
    }

    /// <summary>
    /// When no links exist, the resolved description equals the DishRecord's own description.
    /// Validates: Requirements 1.5, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetDayPlanAsync_NoLinks_ReturnsDishRecordDescription()
    {
        return Prop.ForAll(
            NoLinksScenarioArb(),
            async scenario =>
            {
                // Arrange.
                ResetAllMocks();
                SetupCommonRepositories(scenario.HouseholdId, scenario.Date);
                SetupDishRepository(scenario.HouseholdId, scenario.Date, scenario.DishRecord);
                SetupDayPlanDishLinkRepository(scenario.HouseholdId, scenario.Date, new List<DayPlanDishLink>());
                SetupSavedDishRepository(scenario.HouseholdId, new List<SavedDish>());

                // Act.
                var result = await _sut.GetDayPlanAsync(scenario.HouseholdId, scenario.Date);

                // Assert — description comes from DishRecord directly.
                var descriptionMatches = result.Dish!.Description == scenario.DishRecord!.Description;
                var savedDishIdsNull = result.Dish.SavedDishIds is null;

                return (descriptionMatches && savedDishIdsNull)
                    .Label($"Expected '{scenario.DishRecord.Description}' but got '{result.Dish.Description}'. " +
                           $"SavedDishIds should be null but was {result.Dish.SavedDishIds}");
            });
    }

    /// <summary>
    /// Links referencing soft-deleted SavedDishes still resolve their descriptions.
    /// Validates: Requirements 1.5, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetDayPlanAsync_LinksReferenceSoftDeletedDishes_StillResolvesDescriptions()
    {
        return Prop.ForAll(
            SoftDeletedDishesScenarioArb(),
            async scenario =>
            {
                // Arrange.
                ResetAllMocks();
                SetupCommonRepositories(scenario.HouseholdId, scenario.Date);
                SetupDishRepository(scenario.HouseholdId, scenario.Date, scenario.DishRecord);
                SetupDayPlanDishLinkRepository(scenario.HouseholdId, scenario.Date, scenario.Links);
                SetupSavedDishRepository(scenario.HouseholdId, scenario.SavedDishes);

                // Act.
                var result = await _sut.GetDayPlanAsync(scenario.HouseholdId, scenario.Date);

                // Assert — soft-deleted dishes still contribute to the combined description.
                var expectedDescription = string.Join(" & ",
                    scenario.Links.OrderBy(x => x.SortOrder)
                        .Select(x => scenario.SavedDishes.First(sd => sd.Id == x.SavedDishId).Description));

                var expectedIds = scenario.Links.OrderBy(x => x.SortOrder)
                    .Select(x => x.SavedDishId).ToList();

                var descriptionMatches = result.Dish!.Description == expectedDescription;
                var idsMatch = result.Dish.SavedDishIds != null &&
                    result.Dish.SavedDishIds.SequenceEqual(expectedIds);

                return (descriptionMatches && idsMatch)
                    .Label($"Soft-deleted dishes should resolve. Expected '{expectedDescription}' but got '{result.Dish.Description}'");
            });
    }

    private static Arbitrary<CombinedDescriptionScenario> LinksWithSavedDishesScenarioArb()
    {
        var gen = CreateDateGen().SelectMany(date =>
            Gen.Choose(1, 5).SelectMany(linkCount =>
                Gen.Choose(0, linkCount - 1).SelectMany(missingCount =>
                    CreateSavedDishesGen(linkCount).SelectMany(savedDishes =>
                        CreateLinksWithMissingGen(date, savedDishes, missingCount).Select(result =>
                        {
                            var (links, allSavedDishes, householdId) = result;
                            var dishRecord = new DishRecord(householdId, date, string.Empty, Guid.NewGuid(), DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow);
                            return new CombinedDescriptionScenario(householdId, date, links, allSavedDishes, dishRecord);
                        })))));

        return Arb.From(gen);
    }

    private static Arbitrary<CombinedDescriptionScenario> LinksWithNonEmptyDishDescriptionScenarioArb()
    {
        var gen = CreateDateGen().SelectMany(date =>
            Gen.Choose(1, 5).SelectMany(linkCount =>
                CreateSavedDishesGen(linkCount).SelectMany(savedDishes =>
                    CreateDescriptionGen().Select(dishDescription =>
                    {
                        var householdId = Guid.NewGuid();
                        var links = savedDishes.Select((x, i) =>
                            new DayPlanDishLink(householdId, date, x.Id, i)).ToList();
                        var dishRecord = new DishRecord(householdId, date, dishDescription, Guid.NewGuid(), DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow);
                        return new CombinedDescriptionScenario(householdId, date, links, savedDishes, dishRecord);
                    }))));

        return Arb.From(gen);
    }

    private static Arbitrary<CombinedDescriptionScenario> NoLinksScenarioArb()
    {
        var gen = CreateDateGen().SelectMany(date =>
            CreateDescriptionGen().Select(description =>
            {
                var householdId = Guid.NewGuid();
                var dishRecord = new DishRecord(householdId, date, description, Guid.NewGuid(), DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow);
                return new CombinedDescriptionScenario(householdId, date, new List<DayPlanDishLink>(), new List<SavedDish>(), dishRecord);
            }));

        return Arb.From(gen);
    }

    private static Arbitrary<CombinedDescriptionScenario> SoftDeletedDishesScenarioArb()
    {
        var gen = CreateDateGen().SelectMany(date =>
            Gen.Choose(1, 5).SelectMany(linkCount =>
                CreateSoftDeletedSavedDishesGen(linkCount).Select(savedDishes =>
                {
                    var householdId = Guid.NewGuid();
                    var links = savedDishes.Select((x, i) =>
                        new DayPlanDishLink(householdId, date, x.Id, i)).ToList();
                    var dishRecord = new DishRecord(householdId, date, string.Empty, Guid.NewGuid(), DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow);
                    return new CombinedDescriptionScenario(householdId, date, links, savedDishes, dishRecord);
                })));

        return Arb.From(gen);
    }

    private static Gen<DateOnly> CreateDateGen()
    {
        return Gen.Choose(2020, 2030).SelectMany(year =>
            Gen.Choose(1, 12).SelectMany(month =>
                Gen.Choose(1, 28).Select(day => new DateOnly(year, month, day))));
    }

    private static Gen<string> CreateDescriptionGen()
    {
        var printableCharGen = Gen.Choose(33, 126).Select(x => (char)x);
        return Gen.Choose(1, 50)
            .SelectMany(length => Gen.ListOf(printableCharGen, length)
                .Select(chars => new string(chars.ToArray())));
    }

    private static Gen<List<SavedDish>> CreateSavedDishesGen(int count)
    {
        return CreateDescriptionGen()
            .ListOf(count)
            .Select(descriptions => descriptions
                .Select(x => new SavedDish(Guid.NewGuid(), Guid.NewGuid(), x, false))
                .ToList());
    }

    private static Gen<List<SavedDish>> CreateSoftDeletedSavedDishesGen(int count)
    {
        return CreateDescriptionGen()
            .ListOf(count)
            .Select(descriptions => descriptions
                .Select(x => new SavedDish(Guid.NewGuid(), Guid.NewGuid(), x, true))
                .ToList());
    }

    private static Gen<(List<DayPlanDishLink> Links, List<SavedDish> AllSavedDishes, Guid HouseholdId)> CreateLinksWithMissingGen(
        DateOnly date, List<SavedDish> existingSavedDishes, int missingCount)
    {
        var householdId = Guid.NewGuid();

        // Create links for existing dishes.
        var existingLinks = existingSavedDishes.Select((x, i) =>
            new DayPlanDishLink(householdId, date, x.Id, i)).ToList();

        // Create links for missing dishes (SavedDishId won't exist in the household).
        var missingLinks = Enumerable.Range(0, missingCount)
            .Select(i => new DayPlanDishLink(householdId, date, Guid.NewGuid(), existingSavedDishes.Count + i))
            .ToList();

        var allLinks = existingLinks.Concat(missingLinks).ToList();

        // Shuffle the links to vary sort order.
        return Gen.Shuffle(allLinks.ToArray()).Select(shuffled =>
        {
            // Reassign sort orders based on shuffled position.
            var reorderedLinks = shuffled.Select((x, i) =>
                new DayPlanDishLink(x.HouseholdId, x.Date, x.SavedDishId, i)).ToList();
            return (reorderedLinks, existingSavedDishes, householdId);
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

    private void SetupCommonRepositories(Guid householdId, DateOnly date)
    {
        var housemateId = Guid.NewGuid();
        var housemate = new Housemate(housemateId, householdId, "TestHousemate", "#E91E63", false);

        _housemateRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Housemate> { housemate });

        _attendanceRepositoryMock
            .Setup(x => x.GetByDateAsync(householdId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceRecord>());

        _commentRepositoryMock
            .Setup(x => x.GetByDateAsync(householdId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment>());

        _dayHistoryRepositoryMock
            .Setup(x => x.GetByDateAsync(householdId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DayHistoryEntry>());
    }

    private void SetupDishRepository(Guid householdId, DateOnly date, DishRecord? dishRecord)
    {
        _dishRepositoryMock
            .Setup(x => x.GetAsync(householdId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dishRecord);
    }

    private void SetupDayPlanDishLinkRepository(Guid householdId, DateOnly date, List<DayPlanDishLink> links)
    {
        _dayPlanDishLinkRepositoryMock
            .Setup(x => x.GetByDateAsync(householdId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(links);
    }

    private void SetupSavedDishRepository(Guid householdId, List<SavedDish> savedDishes)
    {
        _savedDishRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedDishes);
    }

    private record CombinedDescriptionScenario(
        Guid HouseholdId,
        DateOnly Date,
        List<DayPlanDishLink> Links,
        List<SavedDish> SavedDishes,
        DishRecord? DishRecord);
}
