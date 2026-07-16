using System.Text.Json;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Domain;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Shared.Domain;
using Moq;

namespace Happie.Api.Tests.Handlers;

// Feature: dinner-time, Property 4: History entry change detection
/// <summary>Property-based tests for history entry change detection in DayHandler.UpsertDishAsync.</summary>
public class DayHandlerHistoryPropertyTests
{
    /// <summary>
    /// For any combination of (oldDescription, newDescription, oldDinnerTime, newDinnerTime),
    /// no history entry is produced when neither the description nor the dinner time changed.
    /// Validates: Requirements 8.1, 8.2, 8.3, 8.6, 8.7
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_NothingChanged_NoHistoryEntry()
    {
        return Prop.ForAll(
            DishDescriptionArb(),
            OptionalTimeArb(),
            async (description, dinnerTime) =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var date = new DateOnly(2025, 1, 15);

                var existingDish = new DishRecord(householdId, date, description, Guid.NewGuid(), DateTimeOffset.UtcNow, dinnerTime, DateTimeOffset.UtcNow);
                var (sut, capturedEntries) = CreateSutWithDishCapture(existingDish);

                // Act.
                await sut.UpsertDishAsync(householdId, date, description, null, dinnerTime, 0, actingHousemateId);

                // Assert.
                return (capturedEntries().Count == 0)
                    .Label($"Expected no history entry when nothing changed, but got {capturedEntries().Count}");
            });
    }

    /// <summary>
    /// For any combination where only the description changed (dinner time stays the same),
    /// exactly one history entry is produced with ChangeType Dish.
    /// Validates: Requirements 8.1, 8.2, 8.3, 8.6, 8.7
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_OnlyDescriptionChanged_DishHistoryEntry()
    {
        return Prop.ForAll(
            TwoDifferentDescriptionsArb(),
            OptionalTimeArb(),
            async (descriptions, dinnerTime) =>
            {
                // Arrange.
                var (oldDescription, newDescription) = descriptions;
                var householdId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var date = new DateOnly(2025, 1, 15);

                var existingDish = new DishRecord(householdId, date, oldDescription, Guid.NewGuid(), DateTimeOffset.UtcNow, dinnerTime, DateTimeOffset.UtcNow);
                var (sut, capturedEntries) = CreateSutWithDishCapture(existingDish);

                // Act.
                await sut.UpsertDishAsync(householdId, date, newDescription, null, dinnerTime, 0, actingHousemateId);

                // Assert.
                var entries = capturedEntries();
                var singleEntry = entries.Count == 1;
                var entry = entries.FirstOrDefault();
                var correctChangeType = entry?.ChangeType == ChangeType.Dish;
                var correctTranslationKey = entry?.TranslationKey == TranslationKeys.HistoryDishSet;
                var parametersContainDescription = ParametersContainKey(entry, "description");

                return (singleEntry && correctChangeType && correctTranslationKey && parametersContainDescription)
                    .Label($"Count={entries.Count}, ChangeType={entry?.ChangeType}, Key={entry?.TranslationKey}, Params={entry?.Parameters}");
            });
    }

    /// <summary>
    /// For any combination where only the dinner time changed (description stays the same)
    /// and the new dinner time is set (not null), exactly one history entry is produced with
    /// ChangeType DinnerTime and translation key history_dinner_time_set.
    /// Validates: Requirements 8.1, 8.2, 8.3, 8.6, 8.7
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_OnlyDinnerTimeSet_DinnerTimeHistoryEntry()
    {
        return Prop.ForAll(
            DishDescriptionArb(),
            TwoDifferentTimesWithNewNotNullArb(),
            async (description, times) =>
            {
                // Arrange.
                var (oldDinnerTime, newDinnerTime) = times;
                var householdId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var date = new DateOnly(2025, 1, 15);

                var existingDish = new DishRecord(householdId, date, description, Guid.NewGuid(), DateTimeOffset.UtcNow, oldDinnerTime, DateTimeOffset.UtcNow);
                var (sut, capturedEntries) = CreateSutWithDishCapture(existingDish);

                // Act.
                await sut.UpsertDishAsync(householdId, date, description, null, newDinnerTime, 0, actingHousemateId);

                // Assert.
                var entries = capturedEntries();
                var singleEntry = entries.Count == 1;
                var entry = entries.FirstOrDefault();
                var correctChangeType = entry?.ChangeType == ChangeType.DinnerTime;
                var correctTranslationKey = entry?.TranslationKey == TranslationKeys.HistoryDinnerTimeSet;
                var parametersContainTime = ParametersContainKey(entry, "time");

                return (singleEntry && correctChangeType && correctTranslationKey && parametersContainTime)
                    .Label($"Count={entries.Count}, ChangeType={entry?.ChangeType}, Key={entry?.TranslationKey}, Params={entry?.Parameters}");
            });
    }

    /// <summary>
    /// For any combination where only the dinner time changed (description stays the same)
    /// and the new dinner time is cleared (null), exactly one history entry is produced with
    /// ChangeType DinnerTime and translation key history_dinner_time_cleared.
    /// Validates: Requirements 8.1, 8.2, 8.3, 8.6, 8.7
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_OnlyDinnerTimeCleared_DinnerTimeClearedHistoryEntry()
    {
        return Prop.ForAll(
            DishDescriptionArb(),
            ValidTimeArb(),
            async (description, oldDinnerTime) =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var date = new DateOnly(2025, 1, 15);

                var existingDish = new DishRecord(householdId, date, description, Guid.NewGuid(), DateTimeOffset.UtcNow, oldDinnerTime, DateTimeOffset.UtcNow);
                var (sut, capturedEntries) = CreateSutWithDishCapture(existingDish);

                // Act.
                await sut.UpsertDishAsync(householdId, date, description, null, null, 0, actingHousemateId);

                // Assert.
                var entries = capturedEntries();
                var singleEntry = entries.Count == 1;
                var entry = entries.FirstOrDefault();
                var correctChangeType = entry?.ChangeType == ChangeType.DinnerTime;
                var correctTranslationKey = entry?.TranslationKey == TranslationKeys.HistoryDinnerTimeCleared;

                return (singleEntry && correctChangeType && correctTranslationKey)
                    .Label($"Count={entries.Count}, ChangeType={entry?.ChangeType}, Key={entry?.TranslationKey}");
            });
    }

    /// <summary>
    /// For any combination where both description and dinner time changed and the new dinner
    /// time is set (not null), exactly one history entry is produced with ChangeType
    /// DishAndDinnerTime and translation key history_dish_and_dinner_time_set.
    /// Validates: Requirements 8.1, 8.2, 8.3, 8.6, 8.7
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_BothChangedDinnerTimeSet_DishAndDinnerTimeHistoryEntry()
    {
        return Prop.ForAll(
            TwoDifferentDescriptionsArb(),
            TwoDifferentTimesWithNewNotNullArb(),
            async (descriptions, times) =>
            {
                // Arrange.
                var (oldDescription, newDescription) = descriptions;
                var (oldDinnerTime, newDinnerTime) = times;
                var householdId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var date = new DateOnly(2025, 1, 15);

                var existingDish = new DishRecord(householdId, date, oldDescription, Guid.NewGuid(), DateTimeOffset.UtcNow, oldDinnerTime, DateTimeOffset.UtcNow);
                var (sut, capturedEntries) = CreateSutWithDishCapture(existingDish);

                // Act.
                await sut.UpsertDishAsync(householdId, date, newDescription, null, newDinnerTime, 0, actingHousemateId);

                // Assert.
                var entries = capturedEntries();
                var singleEntry = entries.Count == 1;
                var entry = entries.FirstOrDefault();
                var correctChangeType = entry?.ChangeType == ChangeType.DishAndDinnerTime;
                var correctTranslationKey = entry?.TranslationKey == TranslationKeys.HistoryDishAndDinnerTimeSet;
                var parametersContainDescription = ParametersContainKey(entry, "description");
                var parametersContainTime = ParametersContainKey(entry, "time");

                return (singleEntry && correctChangeType && correctTranslationKey && parametersContainDescription && parametersContainTime)
                    .Label($"Count={entries.Count}, ChangeType={entry?.ChangeType}, Key={entry?.TranslationKey}, Params={entry?.Parameters}");
            });
    }

    /// <summary>
    /// For any combination where both description and dinner time changed and the new dinner
    /// time is cleared (null), exactly one history entry is produced with ChangeType
    /// DishAndDinnerTime and translation key history_dish_set_dinner_time_cleared.
    /// Validates: Requirements 8.1, 8.2, 8.3, 8.6, 8.7
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_BothChangedDinnerTimeCleared_DishSetDinnerTimeClearedHistoryEntry()
    {
        return Prop.ForAll(
            TwoDifferentDescriptionsArb(),
            ValidTimeArb(),
            async (descriptions, oldDinnerTime) =>
            {
                // Arrange.
                var (oldDescription, newDescription) = descriptions;
                var householdId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var date = new DateOnly(2025, 1, 15);

                var existingDish = new DishRecord(householdId, date, oldDescription, Guid.NewGuid(), DateTimeOffset.UtcNow, oldDinnerTime, DateTimeOffset.UtcNow);
                var (sut, capturedEntries) = CreateSutWithDishCapture(existingDish);

                // Act.
                await sut.UpsertDishAsync(householdId, date, newDescription, null, null, 0, actingHousemateId);

                // Assert.
                var entries = capturedEntries();
                var singleEntry = entries.Count == 1;
                var entry = entries.FirstOrDefault();
                var correctChangeType = entry?.ChangeType == ChangeType.DishAndDinnerTime;
                var correctTranslationKey = entry?.TranslationKey == TranslationKeys.HistoryDishSetDinnerTimeCleared;
                var parametersContainDescription = ParametersContainKey(entry, "description");

                return (singleEntry && correctChangeType && correctTranslationKey && parametersContainDescription)
                    .Label($"Count={entries.Count}, ChangeType={entry?.ChangeType}, Key={entry?.TranslationKey}, Params={entry?.Parameters}");
            });
    }

    /// <summary>
    /// For any combination of inputs, the handler SHALL never produce more than one history
    /// entry per save operation.
    /// Validates: Requirements 8.1, 8.2, 8.3, 8.6, 8.7
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_AnyCombination_AtMostOneHistoryEntry()
    {
        return Prop.ForAll(
            AllChangeInputsArb(),
            async input =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var date = new DateOnly(2025, 1, 15);

                var existingDish = new DishRecord(householdId, date, input.OldDescription, Guid.NewGuid(), DateTimeOffset.UtcNow, input.OldDinnerTime, DateTimeOffset.UtcNow);
                var (sut, capturedEntries) = CreateSutWithDishCapture(existingDish);

                // Act.
                await sut.UpsertDishAsync(householdId, date, input.NewDescription, null, input.NewDinnerTime, 0, actingHousemateId);

                // Assert.
                var entries = capturedEntries();
                return (entries.Count <= 1)
                    .Label($"Expected at most 1 history entry, but got {entries.Count}. Old=({input.OldDescription},{input.OldDinnerTime}), New=({input.NewDescription},{input.NewDinnerTime})");
            });
    }

    private static bool ParametersContainKey(DayHistoryEntry? entry, string key)
    {
        if (entry is null)
            return false;

        var parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(entry.Parameters);
        return parameters != null && parameters.ContainsKey(key);
    }

    private static (DayHandler Sut, Func<List<DayHistoryEntry>> CapturedEntries) CreateSutWithDishCapture(DishRecord? existingDish)
    {
        var housemateRepositoryMock = new Mock<IHousemateRepository>();
        var attendanceRepositoryMock = new Mock<IAttendanceRepository>();
        var dishRepositoryMock = new Mock<IDishRepository>();
        var commentRepositoryMock = new Mock<ICommentRepository>();
        var dayHistoryRepositoryMock = new Mock<IDayHistoryRepository>();
        var pushHandlerMock = new Mock<IPushHandler>();
        var savedDishRepositoryMock = new Mock<ISavedDishRepository>();
        var dayPlanDishLinkRepositoryMock = new Mock<IDayPlanDishLinkRepository>();

        dishRepositoryMock
            .Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDish);

        dishRepositoryMock
            .Setup(x => x.UpsertAsync(It.IsAny<DishRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var captured = new List<DayHistoryEntry>();
        dayHistoryRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<DayHistoryEntry>(), It.IsAny<CancellationToken>()))
            .Callback<DayHistoryEntry, CancellationToken>((entry, _) => captured.Add(entry))
            .Returns(Task.CompletedTask);

        pushHandlerMock
            .Setup(x => x.SendAutoNotificationsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new DayHandler(
            housemateRepositoryMock.Object,
            attendanceRepositoryMock.Object,
            dishRepositoryMock.Object,
            commentRepositoryMock.Object,
            dayHistoryRepositoryMock.Object,
            pushHandlerMock.Object,
            savedDishRepositoryMock.Object,
            dayPlanDishLinkRepositoryMock.Object);

        return (sut, () => captured);
    }

    private static Arbitrary<string> DishDescriptionArb()
    {
        var gen = Gen.Choose(1, 100)
            .SelectMany(len =>
                Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                             'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                             'u', 'v', 'w', 'x', 'y', 'z', ' ', '-')
                    .ArrayOf(len)
                    .Select(chars => new string(chars)));

        return Arb.From(gen);
    }

    private static Arbitrary<TimeOnly> ValidTimeArb()
    {
        var gen = Gen.Choose(0, 23)
            .SelectMany(hour => Gen.Choose(0, 59).Select(minute => new TimeOnly(hour, minute)));

        return Arb.From(gen);
    }

    private static Arbitrary<TimeOnly?> OptionalTimeArb()
    {
        var gen = Gen.Frequency(
            (1, Gen.Constant((TimeOnly?)null)),
            (3, Gen.Choose(0, 23)
                .SelectMany(hour => Gen.Choose(0, 59).Select(minute => (TimeOnly?)new TimeOnly(hour, minute)))));

        return Arb.From(gen);
    }

    private static Arbitrary<(string Old, string New)> TwoDifferentDescriptionsArb()
    {
        var gen = DishDescriptionArb().Generator
            .SelectMany(old => DishDescriptionArb().Generator
                .Where(newDesc => newDesc != old)
                .Select(newDesc => (old, newDesc)));

        return Arb.From(gen);
    }

    private static Arbitrary<(TimeOnly? Old, TimeOnly New)> TwoDifferentTimesWithNewNotNullArb()
    {
        var gen = OptionalTimeArb().Generator
            .SelectMany(old => ValidTimeArb().Generator
                .Where(newTime => (TimeOnly?)newTime != old)
                .Select(newTime => (old, newTime)));

        return Arb.From(gen);
    }

    private static Arbitrary<ChangeInput> AllChangeInputsArb()
    {
        var gen = DishDescriptionArb().Generator
            .SelectMany(oldDesc => DishDescriptionArb().Generator
                .SelectMany(newDesc => OptionalTimeArb().Generator
                    .SelectMany(oldTime => OptionalTimeArb().Generator
                        .Select(newTime => new ChangeInput(oldDesc, newDesc, oldTime, newTime)))));

        return Arb.From(gen);
    }

    private record ChangeInput(string OldDescription, string NewDescription, TimeOnly? OldDinnerTime, TimeOnly? NewDinnerTime);
}
