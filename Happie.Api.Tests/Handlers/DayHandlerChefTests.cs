using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Domain;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Shared.Domain;
using Moq;

namespace Happie.Api.Tests.Handlers;

/// <summary>Property-based tests for chef toggle logic in <see cref="DayHandler"/>.</summary>
public class DayHandlerChefTests
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

    public DayHandlerChefTests()
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

    // Feature: chef-toggle, Property 4: Multiple chefs and cross-housemate toggling
    /// <summary>
    /// For any non-empty subset of active housemates in a household, enabling chef status for each
    /// housemate in the subset (with any acting housemate) should result in all housemates in the
    /// subset having IsChef = true in the stored state.
    /// Validates: Requirements 2.5, 2.6, 3.1, 3.3, 4.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertChefStatusAsync_MultipleChefs_AllSubsetMembersAreChef()
    {
        return Prop.ForAll(
            MultipleChefArb(),
            async args =>
            {
                var (householdId, date, housemates, chefSubset, actingHousemateId) = args;

                // Arrange.
                // Use a dictionary to simulate attendance storage.
                var storage = new Dictionary<Guid, AttendanceRecord>();

                _housemateRepositoryMock.Reset();
                _attendanceRepositoryMock.Reset();
                _dayHistoryRepositoryMock.Reset();

                _housemateRepositoryMock
                    .Setup(x => x.GetAsync(householdId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Guid hId, Guid hmId, CancellationToken _) =>
                        housemates.FirstOrDefault(x => x.Id == hmId));

                _attendanceRepositoryMock
                    .Setup(x => x.UpsertChefStatusAsync(householdId, date, It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                    .Callback((Guid hId, DateOnly d, Guid hmId, bool isChef, CancellationToken _) =>
                    {
                        if (storage.TryGetValue(hmId, out var existing))
                            storage[hmId] = existing with { IsChef = isChef };
                        else
                            storage[hmId] = new AttendanceRecord(hId, hmId, d, AttendanceStatus.Unknown, isChef, DateTimeOffset.UtcNow);
                    })
                    .Returns(Task.CompletedTask);

                _dayHistoryRepositoryMock
                    .Setup(x => x.AddAsync(It.IsAny<DayHistoryEntry>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                // Act.
                foreach (var targetId in chefSubset)
                {
                    await _sut.UpsertChefStatusAsync(householdId, date, targetId, true, actingHousemateId);
                }

                // Assert.
                var allSubsetAreChef = chefSubset.All(x => storage.ContainsKey(x) && storage[x].IsChef);

                return allSubsetAreChef
                    .Label($"Expected all {chefSubset.Count} subset members to have IsChef=true, " +
                           $"but found {chefSubset.Count(x => storage.ContainsKey(x) && storage[x].IsChef)} with IsChef=true");
            });
    }

    // Feature: chef-toggle, Property 5: Chef toggle creates correctly attributed history entry
    /// <summary>
    /// For any chef status toggle by any acting housemate for any target housemate on any date,
    /// the system should create a DayHistory entry with ChangeType.ChefStatusChanged,
    /// ChangedByHousemateId equal to the acting housemate's ID, and the entry associated with the target date.
    /// Validates: Requirements 4.2, 8.1, 8.2, 8.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertChefStatusAsync_AnyToggle_CreatesCorrectlyAttributedHistoryEntry()
    {
        return Prop.ForAll(
            ChefHistoryArb(),
            async args =>
            {
                var (householdId, housemateId, actingHousemateId, date, isChef) = args;

                // Arrange.
                _housemateRepositoryMock.Reset();
                _attendanceRepositoryMock.Reset();
                _dayHistoryRepositoryMock.Reset();

                var housemate = new Housemate(housemateId, householdId, "TestHousemate", "#FF0000", false);
                _housemateRepositoryMock
                    .Setup(x => x.GetAsync(householdId, housemateId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(housemate);

                _attendanceRepositoryMock
                    .Setup(x => x.UpsertChefStatusAsync(householdId, date, housemateId, isChef, It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                DayHistoryEntry? capturedEntry = null;
                _dayHistoryRepositoryMock
                    .Setup(x => x.AddAsync(It.IsAny<DayHistoryEntry>(), It.IsAny<CancellationToken>()))
                    .Callback<DayHistoryEntry, CancellationToken>((entry, _) => capturedEntry = entry)
                    .Returns(Task.CompletedTask);

                // Act.
                await _sut.UpsertChefStatusAsync(householdId, date, housemateId, isChef, actingHousemateId);

                // Assert.
                var entryCreated = (capturedEntry != null)
                    .Label("Expected a DayHistoryEntry to be created");
                var correctChangeType = (capturedEntry?.ChangeType == ChangeType.ChefStatusChanged)
                    .Label($"Expected ChangeType.ChefStatusChanged but got {capturedEntry?.ChangeType}");
                var correctActingHousemate = (capturedEntry?.ChangedByHousemateId == actingHousemateId)
                    .Label($"Expected ChangedByHousemateId={actingHousemateId} but got {capturedEntry?.ChangedByHousemateId}");
                var correctDate = (capturedEntry?.Date == date)
                    .Label($"Expected Date={date} but got {capturedEntry?.Date}");

                return entryCreated
                    .And(correctChangeType)
                    .And(correctActingHousemate)
                    .And(correctDate);
            });
    }

    private static Arbitrary<(Guid HouseholdId, DateOnly Date, List<Housemate> Housemates, List<Guid> ChefSubset, Guid ActingHousemateId)> MultipleChefArb()
    {
        var householdIdGen = ArbMap.Default.GeneratorFor<Guid>();
        // FsCheck does not handle DateOnly automatically; generate from a day offset.
        var dateGen = Gen.Choose(0, 365).Select(x => DateOnly.FromDayNumber(738000 + x));

        var gen = householdIdGen.SelectMany(householdId =>
            dateGen.SelectMany(date =>
                Gen.Choose(2, 5).SelectMany(count =>
                {
                    // Generate exactly count unique housemate IDs.
                    var uniqueIds = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

                    var housemates = uniqueIds
                        .Select((id, index) => new Housemate(
                            id,
                            householdId,
                            $"Housemate{index}",
                            HousemateColors.Palette[index % HousemateColors.Palette.Count],
                            false))
                        .ToList();

                    // Generate a non-empty subset by including each housemate with probability 0.5, ensuring at least one.
                    var subsetGen = Gen.SubListOf(uniqueIds.ToArray())
                        .Where(x => x.Count > 0)
                        .Select(x => x.ToList());

                    // Acting housemate can be any housemate (cross-housemate toggling).
                    return subsetGen.SelectMany(chefSubset =>
                        Gen.Elements(uniqueIds.ToArray()).Select(actingId =>
                            (householdId, date, housemates, chefSubset, actingId)));
                })));

        return Arb.From(gen);
    }

    private static Arbitrary<(Guid HouseholdId, Guid HousemateId, Guid ActingHousemateId, DateOnly Date, bool IsChef)> ChefHistoryArb()
    {
        var guidGen = ArbMap.Default.GeneratorFor<Guid>();
        // FsCheck does not handle DateOnly automatically; generate from a day offset.
        var dateGen = Gen.Choose(0, 365).Select(x => DateOnly.FromDayNumber(738000 + x));
        var boolGen = ArbMap.Default.GeneratorFor<bool>();

        var gen = guidGen.SelectMany(householdId =>
            guidGen.SelectMany(housemateId =>
                guidGen.SelectMany(actingHousemateId =>
                    dateGen.SelectMany(date =>
                        boolGen.Select(isChef =>
                            (householdId, housemateId, actingHousemateId, date, isChef))))));

        return Arb.From(gen);
    }
}
