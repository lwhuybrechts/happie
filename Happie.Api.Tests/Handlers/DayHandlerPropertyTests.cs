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

// Feature: history-translation, Property 1: Write path produces valid structured entries
/// <summary>Property-based tests for <see cref="DayHandler"/> write path structured entries.</summary>
public class DayHandlerPropertyTests
{
    private static readonly HashSet<string> KnownHistoryKeys = new()
    {
        TranslationKeys.HistoryAttendanceSet,
        TranslationKeys.HistoryDishSet,
        TranslationKeys.HistoryCommentSet,
        TranslationKeys.HistoryCommentDeleted,
        TranslationKeys.HistoryChefStatusChanged,
    };

    private static readonly Dictionary<string, HashSet<string>> ExpectedParameterKeys = new()
    {
        [TranslationKeys.HistoryAttendanceSet] = new() { "name", "status" },
        [TranslationKeys.HistoryDishSet] = new() { "description" },
        [TranslationKeys.HistoryCommentSet] = new() { "name", "text" },
        [TranslationKeys.HistoryCommentDeleted] = new() { "name" },
        [TranslationKeys.HistoryChefStatusChanged] = new() { "name", "enabled" },
    };

    /// <summary>
    /// For any valid attendance change with a non-empty name and any AttendanceStatus,
    /// the stored DayHistoryEntry has a known history TranslationKey and Parameters
    /// JSON containing exactly the expected placeholder keys.
    /// Validates: Requirements 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 1.10
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertAttendanceAsync_AnyValidInput_StoresValidStructuredEntry()
    {
        return Prop.ForAll(
            NonEmptyNameArb(),
            AttendanceStatusArb(),
            async (name, status) =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var housemateId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var date = new DateOnly(2025, 1, 15);

                var (sut, capturedEntry) = CreateSutWithCapture(householdId, housemateId, name);

                // Act.
                await sut.UpsertAttendanceAsync(householdId, date, housemateId, status, actingHousemateId);

                // Assert.
                return ValidateStructuredEntry(capturedEntry(), TranslationKeys.HistoryAttendanceSet, name, status);
            });
    }

    /// <summary>
    /// For any valid dish description (1–100 chars), the stored DayHistoryEntry has the correct
    /// TranslationKey and Parameters JSON with the expected placeholder keys.
    /// Validates: Requirements 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 1.10
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_AnyValidInput_StoresValidStructuredEntry()
    {
        return Prop.ForAll(
            DishDescriptionArb(),
            async description =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var date = new DateOnly(2025, 1, 15);

                var (sut, capturedEntry) = CreateSutForDish();

                // Act.
                await sut.UpsertDishAsync(householdId, date, description, null, 0, actingHousemateId);

                // Assert.
                var entry = capturedEntry();
                var keyValid = entry != null && KnownHistoryKeys.Contains(entry.TranslationKey);
                var keyCorrect = entry?.TranslationKey == TranslationKeys.HistoryDishSet;
                var paramsValid = ValidateParameterKeys(entry);

                return (keyValid && keyCorrect && paramsValid)
                    .Label($"Key={entry?.TranslationKey}, Params={entry?.Parameters}");
            });
    }

    /// <summary>
    /// For any valid comment set with a non-empty name and text (1–200 chars), the stored
    /// DayHistoryEntry has the correct TranslationKey and Parameters JSON with expected keys.
    /// Validates: Requirements 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 1.10
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertCommentAsync_AnyValidInput_StoresValidStructuredEntry()
    {
        return Prop.ForAll(
            NonEmptyNameArb(),
            CommentTextArb(),
            async (name, text) =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var housemateId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var date = new DateOnly(2025, 1, 15);

                var (sut, capturedEntry) = CreateSutWithCapture(householdId, housemateId, name);

                // Act.
                await sut.UpsertCommentAsync(householdId, date, housemateId, text, actingHousemateId);

                // Assert.
                var entry = capturedEntry();
                var keyValid = entry != null && KnownHistoryKeys.Contains(entry.TranslationKey);
                var keyCorrect = entry?.TranslationKey == TranslationKeys.HistoryCommentSet;
                var paramsValid = ValidateParameterKeys(entry);

                return (keyValid && keyCorrect && paramsValid)
                    .Label($"Key={entry?.TranslationKey}, Params={entry?.Parameters}");
            });
    }

    /// <summary>
    /// For any valid comment delete with a non-empty name, the stored DayHistoryEntry has the
    /// correct TranslationKey and Parameters JSON with expected keys.
    /// Validates: Requirements 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 1.10
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeleteCommentAsync_AnyValidInput_StoresValidStructuredEntry()
    {
        return Prop.ForAll(
            NonEmptyNameArb(),
            async name =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var housemateId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var date = new DateOnly(2025, 1, 15);

                var (sut, capturedEntry) = CreateSutWithCapture(householdId, housemateId, name);

                // Act.
                await sut.DeleteCommentAsync(householdId, date, housemateId, actingHousemateId);

                // Assert.
                var entry = capturedEntry();
                var keyValid = entry != null && KnownHistoryKeys.Contains(entry.TranslationKey);
                var keyCorrect = entry?.TranslationKey == TranslationKeys.HistoryCommentDeleted;
                var paramsValid = ValidateParameterKeys(entry);

                return (keyValid && keyCorrect && paramsValid)
                    .Label($"Key={entry?.TranslationKey}, Params={entry?.Parameters}");
            });
    }

    /// <summary>
    /// For any valid chef status change with a non-empty name and any boolean, the stored
    /// DayHistoryEntry has the correct TranslationKey and Parameters JSON with expected keys.
    /// Validates: Requirements 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 1.10
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertChefStatusAsync_AnyValidInput_StoresValidStructuredEntry()
    {
        return Prop.ForAll(
            NonEmptyNameArb(),
            BoolArb(),
            async (name, isChef) =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var housemateId = Guid.NewGuid();
                var actingHousemateId = Guid.NewGuid();
                var date = new DateOnly(2025, 1, 15);

                var (sut, capturedEntry) = CreateSutWithCapture(householdId, housemateId, name);

                // Act.
                await sut.UpsertChefStatusAsync(householdId, date, housemateId, isChef, actingHousemateId);

                // Assert.
                var entry = capturedEntry();
                var keyValid = entry != null && KnownHistoryKeys.Contains(entry.TranslationKey);
                var keyCorrect = entry?.TranslationKey == TranslationKeys.HistoryChefStatusChanged;
                var paramsValid = ValidateParameterKeys(entry);

                return (keyValid && keyCorrect && paramsValid)
                    .Label($"Key={entry?.TranslationKey}, Params={entry?.Parameters}");
            });
    }

    private static bool ValidateParameterKeys(DayHistoryEntry? entry)
    {
        if (entry is null)
            return false;

        if (!ExpectedParameterKeys.TryGetValue(entry.TranslationKey, out var expectedKeys))
            return false;

        var parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(entry.Parameters);
        if (parameters is null)
            return false;

        // Verify exact key match: no missing keys and no extra keys.
        return expectedKeys.SetEquals(parameters.Keys);
    }

    private static Property ValidateStructuredEntry(DayHistoryEntry? entry, string expectedKey, string name, AttendanceStatus status)
    {
        var keyValid = entry != null && KnownHistoryKeys.Contains(entry.TranslationKey);
        var keyCorrect = entry?.TranslationKey == expectedKey;
        var paramsValid = ValidateParameterKeys(entry);

        return (keyValid && keyCorrect && paramsValid)
            .Label($"Key={entry?.TranslationKey}, Params={entry?.Parameters}");
    }

    private static (DayHandler Sut, Func<DayHistoryEntry?> CapturedEntry) CreateSutWithCapture(Guid householdId, Guid housemateId, string name)
    {
        var housemateRepositoryMock = new Mock<IHousemateRepository>();
        var attendanceRepositoryMock = new Mock<IAttendanceRepository>();
        var dishRepositoryMock = new Mock<IDishRepository>();
        var commentRepositoryMock = new Mock<ICommentRepository>();
        var dayHistoryRepositoryMock = new Mock<IDayHistoryRepository>();
        var pushHandlerMock = new Mock<IPushHandler>();
        var savedDishRepositoryMock = new Mock<ISavedDishRepository>();

        var housemate = new Housemate(housemateId, householdId, name, HousemateColors.Palette[0], false);

        housemateRepositoryMock
            .Setup(x => x.GetAsync(householdId, housemateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(housemate);

        attendanceRepositoryMock
            .Setup(x => x.GetAsync(householdId, It.IsAny<DateOnly>(), housemateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceRecord?)null);

        attendanceRepositoryMock
            .Setup(x => x.UpsertAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        attendanceRepositoryMock
            .Setup(x => x.UpsertChefStatusAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        commentRepositoryMock
            .Setup(x => x.UpsertAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        commentRepositoryMock
            .Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        DayHistoryEntry? captured = null;
        dayHistoryRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<DayHistoryEntry>(), It.IsAny<CancellationToken>()))
            .Callback<DayHistoryEntry, CancellationToken>((entry, _) => captured = entry)
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
            savedDishRepositoryMock.Object);

        return (sut, () => captured);
    }

    private static (DayHandler Sut, Func<DayHistoryEntry?> CapturedEntry) CreateSutForDish()
    {
        var housemateRepositoryMock = new Mock<IHousemateRepository>();
        var attendanceRepositoryMock = new Mock<IAttendanceRepository>();
        var dishRepositoryMock = new Mock<IDishRepository>();
        var commentRepositoryMock = new Mock<ICommentRepository>();
        var dayHistoryRepositoryMock = new Mock<IDayHistoryRepository>();
        var pushHandlerMock = new Mock<IPushHandler>();
        var savedDishRepositoryMock = new Mock<ISavedDishRepository>();

        dishRepositoryMock
            .Setup(x => x.UpsertAsync(It.IsAny<DishRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        DayHistoryEntry? captured = null;
        dayHistoryRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<DayHistoryEntry>(), It.IsAny<CancellationToken>()))
            .Callback<DayHistoryEntry, CancellationToken>((entry, _) => captured = entry)
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
            savedDishRepositoryMock.Object);

        return (sut, () => captured);
    }

    private static Arbitrary<string> NonEmptyNameArb()
    {
        var gen = Gen.Choose(1, 50)
            .SelectMany(len =>
                Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                             'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                             'u', 'v', 'w', 'x', 'y', 'z', 'A', 'B', 'C', 'D')
                    .ArrayOf(len)
                    .Select(chars => new string(chars)));

        return Arb.From(gen);
    }

    private static Arbitrary<AttendanceStatus> AttendanceStatusArb()
    {
        var gen = Gen.Elements(AttendanceStatus.Unknown, AttendanceStatus.EatingIn, AttendanceStatus.NotEatingIn);
        return Arb.From(gen);
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

    private static Arbitrary<string> CommentTextArb()
    {
        var gen = Gen.Choose(1, 200)
            .SelectMany(len =>
                Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                             'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                             'u', 'v', 'w', 'x', 'y', 'z', ' ', '.', '!', '?')
                    .ArrayOf(len)
                    .Select(chars => new string(chars)));

        return Arb.From(gen);
    }

    private static Arbitrary<bool> BoolArb()
    {
        var gen = Gen.Elements(true, false);
        return Arb.From(gen);
    }
}
