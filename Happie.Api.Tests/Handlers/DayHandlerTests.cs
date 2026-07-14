using System.Text.Json;
using ExpectedObjects;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Shared.Contracts;
using Happie.Api.Domain;
using Happie.Shared.Domain;
using Moq;

namespace Happie.Api.Tests.Handlers;

/// <summary>Unit tests for <see cref="DayHandler"/>.</summary>
public class DayHandlerTests
{
    private readonly Mock<IHousemateRepository> _housemateRepositoryMock = new();
    private readonly Mock<IAttendanceRepository> _attendanceRepositoryMock = new();
    private readonly Mock<IDishRepository> _dishRepositoryMock = new();
    private readonly Mock<ICommentRepository> _commentRepositoryMock = new();
    private readonly Mock<IDayHistoryRepository> _dayHistoryRepositoryMock = new();
    private readonly Mock<IPushHandler> _pushHandlerMock = new();
    private readonly DayHandler _sut;

    /// <summary>Initializes a new instance of <see cref="DayHandlerTests"/> with mocked dependencies.</summary>
    public DayHandlerTests()
    {
        _pushHandlerMock
            .Setup(x => x.SendAutoNotificationsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new DayHandler(
            _housemateRepositoryMock.Object,
            _attendanceRepositoryMock.Object,
            _dishRepositoryMock.Object,
            _commentRepositoryMock.Object,
            _dayHistoryRepositoryMock.Object,
            _pushHandlerMock.Object);
    }

    /// <summary>A dish description of exactly 100 characters is accepted and saved.</summary>
    [Fact]
    public async Task UpsertDishAsync_DescriptionExactly100Chars_SavesSuccessfully()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var description = new string('A', 100);

        SetupDishUpsert();
        SetupHistoryAdd();

        // Act.
        await _sut.UpsertDishAsync(householdId, date, description, null, 0, actingHousemateId);

        // Assert.
        _dishRepositoryMock.Verify(
            x => x.UpsertAsync(It.Is<DishRecord>(r => r.Description == description && r.LastChangedByHousemateId == actingHousemateId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>A comment text of exactly 200 characters is accepted and saved.</summary>
    [Fact]
    public async Task UpsertCommentAsync_TextExactly200Chars_SavesSuccessfully()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var text = new string('A', 200);

        SetupGetHousemate(householdId, housemateId, CreateHousemate(householdId, housemateId));
        SetupCommentUpsert();
        SetupHistoryAdd();

        // Act.
        var result = await _sut.UpsertCommentAsync(householdId, date, housemateId, text, actingHousemateId);

        // Assert.
        Assert.True(result);
        _commentRepositoryMock.Verify(
            x => x.UpsertAsync(It.Is<Comment>(c => c.Text == text), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>History entries returned by the repository are already in reverse-chronological order.</summary>
    [Fact]
    public async Task GetDayPlanAsync_HistoryEntries_ReturnedInReverseChronologicalOrder()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);

        var t1 = new DateTimeOffset(2025, 7, 15, 10, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2025, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var t3 = new DateTimeOffset(2025, 7, 15, 14, 0, 0, TimeSpan.Zero);

        // Repository returns entries already in reverse-chronological order (most recent first).
        var historyEntries = new List<DayHistoryEntry>
        {
            new(householdId, date, t3, housemateId, ChangeType.Comment, TranslationKeys.HistoryCommentSet, """{"name":"Alice","text":"Hello"}"""),
            new(householdId, date, t2, housemateId, ChangeType.Dish, TranslationKeys.HistoryDishSet, """{"description":"Pasta"}"""),
            new(householdId, date, t1, housemateId, ChangeType.Attendance, TranslationKeys.HistoryAttendanceSet, """{"name":"Alice","status":"EatingIn"}"""),
        };

        var housemate = CreateHousemate(householdId, housemateId);

        SetupGetAllHousemates(householdId, new List<Housemate> { housemate });
        SetupGetAttendanceByDate(householdId, date, new List<AttendanceRecord>());
        SetupGetDish(householdId, date, null);
        SetupGetCommentsByDate(householdId, date, new List<Comment>());
        SetupGetHistoryByDate(householdId, date, historyEntries);

        // Act.
        var result = await _sut.GetDayPlanAsync(householdId, date);

        // Assert.
        var expectedHistory = new List<HistoryEntryDto>
        {
            new(t3, housemateId, housemate.Name, ChangeType.Comment, TranslationKeys.HistoryCommentSet, """{"name":"Alice","text":"Hello"}"""),
            new(t2, housemateId, housemate.Name, ChangeType.Dish, TranslationKeys.HistoryDishSet, """{"description":"Pasta"}"""),
            new(t1, housemateId, housemate.Name, ChangeType.Attendance, TranslationKeys.HistoryAttendanceSet, """{"name":"Alice","status":"EatingIn"}"""),
        };

        expectedHistory.ToExpectedObject().ShouldEqual(result.History);
    }

    /// <summary>When the housemate does not exist, UpsertCommentAsync returns false.</summary>
    [Fact]
    public async Task UpsertCommentAsync_HousemateNotFound_ReturnsFalse()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);

        SetupGetHousemate(householdId, housemateId, null);

        // Act.
        var result = await _sut.UpsertCommentAsync(householdId, date, housemateId, "Hello", Guid.NewGuid());

        // Assert.
        Assert.False(result);
    }

    /// <summary>When the housemate does not exist, DeleteCommentAsync returns false.</summary>
    [Fact]
    public async Task DeleteCommentAsync_HousemateNotFound_ReturnsFalse()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);

        SetupGetHousemate(householdId, housemateId, null);

        // Act.
        var result = await _sut.DeleteCommentAsync(householdId, date, housemateId, Guid.NewGuid());

        // Assert.
        Assert.False(result);
    }

    /// <summary>When the housemate exists, DeleteCommentAsync deletes the comment and writes a history entry.</summary>
    [Fact]
    public async Task DeleteCommentAsync_HousemateExists_DeletesCommentAndWritesHistory()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);

        SetupGetHousemate(householdId, housemateId, CreateHousemate(householdId, housemateId));
        SetupCommentDelete();
        SetupHistoryAdd();

        // Act.
        var result = await _sut.DeleteCommentAsync(householdId, date, housemateId, actingHousemateId);

        // Assert.
        Assert.True(result);
        _commentRepositoryMock.Verify(
            x => x.DeleteAsync(householdId, date, housemateId, It.IsAny<CancellationToken>()),
            Times.Once);
        _dayHistoryRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<DayHistoryEntry>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>When no dish exists, DeleteDishAsync does not delete or write history.</summary>
    [Fact]
    public async Task DeleteDishAsync_NoDishExists_DoesNotDeleteOrWriteHistory()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);

        SetupGetDish(householdId, date, null);

        // Act.
        await _sut.DeleteDishAsync(householdId, date, actingHousemateId);

        // Assert.
        _dishRepositoryMock.Verify(
            x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _dayHistoryRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<DayHistoryEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>When a dish exists, DeleteDishAsync deletes the record and writes a history entry.</summary>
    [Fact]
    public async Task DeleteDishAsync_DishExists_DeletesAndWritesHistory()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var existingDish = new DishRecord(householdId, date, "Pasta", actingHousemateId, DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow, null);

        SetupGetDish(householdId, date, existingDish);
        SetupDishDelete();
        SetupHistoryAdd();

        // Act.
        await _sut.DeleteDishAsync(householdId, date, actingHousemateId);

        // Assert.
        _dishRepositoryMock.Verify(
            x => x.DeleteAsync(householdId, date, It.IsAny<CancellationToken>()),
            Times.Once);
        _dayHistoryRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<DayHistoryEntry>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>DeleteDishAsync stores the correct TranslationKey and Parameters.</summary>
    [Fact]
    public async Task DeleteDishAsync_DishExists_StoresCorrectTranslationKeyAndParameters()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var existingDish = new DishRecord(householdId, date, "Pasta", actingHousemateId, DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow, null);

        SetupGetDish(householdId, date, existingDish);
        SetupDishDelete();

        DayHistoryEntry? capturedEntry = null;
        SetupHistoryAddWithCapture(entry => capturedEntry = entry);

        // Act.
        await _sut.DeleteDishAsync(householdId, date, actingHousemateId);

        // Assert.
        Assert.NotNull(capturedEntry);
        Assert.Equal(TranslationKeys.HistoryDishDeleted, capturedEntry.TranslationKey);
        Assert.Equal("{}", capturedEntry.Parameters);
        Assert.Equal(ChangeType.Dish, capturedEntry.ChangeType);
        Assert.Equal(actingHousemateId, capturedEntry.ChangedByHousemateId);
    }

    /// <summary>UpsertAttendanceAsync stores the correct TranslationKey and Parameters.</summary>
    [Fact]
    public async Task UpsertAttendanceAsync_ValidInput_StoresCorrectTranslationKeyAndParameters()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var status = AttendanceStatus.EatingIn;
        var housemate = CreateHousemate(householdId, housemateId);

        SetupGetHousemate(householdId, housemateId, housemate);
        SetupGetAttendance(householdId, date, housemateId, null);
        SetupAttendanceUpsert();

        DayHistoryEntry? capturedEntry = null;
        SetupHistoryAddWithCapture(entry => capturedEntry = entry);

        // Act.
        await _sut.UpsertAttendanceAsync(householdId, date, housemateId, status, actingHousemateId);

        // Assert.
        Assert.NotNull(capturedEntry);
        Assert.Equal(TranslationKeys.HistoryAttendanceSet, capturedEntry.TranslationKey);
        var parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(capturedEntry.Parameters)!;
        new Dictionary<string, string> { ["name"] = housemateId.ToString(), ["status"] = "EatingIn" }
            .ToExpectedObject()
            .ShouldEqual(parameters);
    }

    /// <summary>UpsertDishAsync stores the correct TranslationKey and Parameters.</summary>
    [Fact]
    public async Task UpsertDishAsync_ValidInput_StoresCorrectTranslationKeyAndParameters()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var description = "Spaghetti Bolognese";

        SetupDishUpsert();

        DayHistoryEntry? capturedEntry = null;
        SetupHistoryAddWithCapture(entry => capturedEntry = entry);

        // Act.
        await _sut.UpsertDishAsync(householdId, date, description, null, 0, actingHousemateId);

        // Assert.
        Assert.NotNull(capturedEntry);
        Assert.Equal(TranslationKeys.HistoryDishSet, capturedEntry.TranslationKey);
        var parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(capturedEntry.Parameters)!;
        new Dictionary<string, string> { ["description"] = description }
            .ToExpectedObject()
            .ShouldEqual(parameters);
    }

    /// <summary>UpsertCommentAsync stores the correct TranslationKey and Parameters.</summary>
    [Fact]
    public async Task UpsertCommentAsync_ValidInput_StoresCorrectTranslationKeyAndParameters()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var text = "Looks delicious!";
        var housemate = CreateHousemate(householdId, housemateId);

        SetupGetHousemate(householdId, housemateId, housemate);
        SetupCommentUpsert();

        DayHistoryEntry? capturedEntry = null;
        SetupHistoryAddWithCapture(entry => capturedEntry = entry);

        // Act.
        await _sut.UpsertCommentAsync(householdId, date, housemateId, text, actingHousemateId);

        // Assert.
        Assert.NotNull(capturedEntry);
        Assert.Equal(TranslationKeys.HistoryCommentSet, capturedEntry.TranslationKey);
        var parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(capturedEntry.Parameters)!;
        new Dictionary<string, string> { ["name"] = housemateId.ToString(), ["text"] = text }
            .ToExpectedObject()
            .ShouldEqual(parameters);
    }

    /// <summary>DeleteCommentAsync stores the correct TranslationKey and Parameters.</summary>
    [Fact]
    public async Task DeleteCommentAsync_ValidInput_StoresCorrectTranslationKeyAndParameters()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var housemate = CreateHousemate(householdId, housemateId);

        SetupGetHousemate(householdId, housemateId, housemate);
        SetupCommentDelete();

        DayHistoryEntry? capturedEntry = null;
        SetupHistoryAddWithCapture(entry => capturedEntry = entry);

        // Act.
        await _sut.DeleteCommentAsync(householdId, date, housemateId, actingHousemateId);

        // Assert.
        Assert.NotNull(capturedEntry);
        Assert.Equal(TranslationKeys.HistoryCommentDeleted, capturedEntry.TranslationKey);
        var parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(capturedEntry.Parameters)!;
        new Dictionary<string, string> { ["name"] = housemateId.ToString() }
            .ToExpectedObject()
            .ShouldEqual(parameters);
    }

    /// <summary>UpsertChefStatusAsync stores the correct TranslationKey and Parameters.</summary>
    [Fact]
    public async Task UpsertChefStatusAsync_ValidInput_StoresCorrectTranslationKeyAndParameters()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var isChef = true;
        var housemate = CreateHousemate(householdId, housemateId);

        SetupGetHousemate(householdId, housemateId, housemate);
        SetupChefStatusUpsert();

        DayHistoryEntry? capturedEntry = null;
        SetupHistoryAddWithCapture(entry => capturedEntry = entry);

        // Act.
        await _sut.UpsertChefStatusAsync(householdId, date, housemateId, isChef, actingHousemateId);

        // Assert.
        Assert.NotNull(capturedEntry);
        Assert.Equal(TranslationKeys.HistoryChefStatusChanged, capturedEntry.TranslationKey);
        var parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(capturedEntry.Parameters)!;
        new Dictionary<string, string> { ["name"] = housemateId.ToString(), ["enabled"] = "true" }
            .ToExpectedObject()
            .ShouldEqual(parameters);
    }

    /// <summary>UpsertChefStatusAsync with isChef=false stores enabled parameter as "false".</summary>
    [Fact]
    public async Task UpsertChefStatusAsync_DisableChef_StoresEnabledFalse()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var actingHousemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var housemate = CreateHousemate(householdId, housemateId);

        SetupGetHousemate(householdId, housemateId, housemate);
        SetupChefStatusUpsert();

        DayHistoryEntry? capturedEntry = null;
        SetupHistoryAddWithCapture(entry => capturedEntry = entry);

        // Act.
        await _sut.UpsertChefStatusAsync(householdId, date, housemateId, false, actingHousemateId);

        // Assert.
        Assert.NotNull(capturedEntry);
        Assert.Equal(TranslationKeys.HistoryChefStatusChanged, capturedEntry.TranslationKey);
        var parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(capturedEntry.Parameters)!;
        new Dictionary<string, string> { ["name"] = housemateId.ToString(), ["enabled"] = "false" }
            .ToExpectedObject()
            .ShouldEqual(parameters);
    }

    /// <summary>GetDayPlanAsync returns raw TranslationKey and Parameters in HistoryEntryDto without resolution.</summary>
    [Fact]
    public async Task GetDayPlanAsync_HistoryEntries_ReturnsRawTranslationKeyAndParameters()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var changedAt = new DateTimeOffset(2025, 7, 15, 10, 0, 0, TimeSpan.Zero);

        var translationKey = TranslationKeys.HistoryAttendanceSet;
        var parametersJson = """{"name":"Alice","status":"EatingIn"}""";

        var historyEntries = new List<DayHistoryEntry>
        {
            new(householdId, date, changedAt, housemateId, ChangeType.Attendance, translationKey, parametersJson),
        };

        var housemate = CreateHousemate(householdId, housemateId);

        SetupGetAllHousemates(householdId, new List<Housemate> { housemate });
        SetupGetAttendanceByDate(householdId, date, new List<AttendanceRecord>());
        SetupGetDish(householdId, date, null);
        SetupGetCommentsByDate(householdId, date, new List<Comment>());
        SetupGetHistoryByDate(householdId, date, historyEntries);

        // Act.
        var result = await _sut.GetDayPlanAsync(householdId, date);

        // Assert.
        var historyDto = Assert.Single(result.History);
        Assert.Equal(translationKey, historyDto.TranslationKey);
        Assert.Equal(parametersJson, historyDto.Parameters);
    }

    /// <summary>GetDayPlanAsync resolves GUID-based "name" parameter to the housemate's current name.</summary>
    [Fact]
    public async Task GetDayPlanAsync_HistoryWithGuidName_ResolvesToCurrentHousemateName()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var housemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var changedAt = new DateTimeOffset(2025, 7, 15, 10, 0, 0, TimeSpan.Zero);

        var translationKey = TranslationKeys.HistoryAttendanceSet;
        var parametersJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["name"] = housemateId.ToString(),
            ["status"] = "EatingIn"
        });

        var historyEntries = new List<DayHistoryEntry>
        {
            new(householdId, date, changedAt, housemateId, ChangeType.Attendance, translationKey, parametersJson),
        };

        var housemate = CreateHousemate(householdId, housemateId);

        SetupGetAllHousemates(householdId, new List<Housemate> { housemate });
        SetupGetAttendanceByDate(householdId, date, new List<AttendanceRecord>());
        SetupGetDish(householdId, date, null);
        SetupGetCommentsByDate(householdId, date, new List<Comment>());
        SetupGetHistoryByDate(householdId, date, historyEntries);

        // Act.
        var result = await _sut.GetDayPlanAsync(householdId, date);

        // Assert.
        var historyDto = Assert.Single(result.History);
        var resolvedParams = JsonSerializer.Deserialize<Dictionary<string, string>>(historyDto.Parameters)!;
        Assert.Equal("Alice", resolvedParams["name"]);
        Assert.Equal("EatingIn", resolvedParams["status"]);
    }

    private void SetupGetHousemate(Guid householdId, Guid housemateId, Housemate? returns)
    {
        _housemateRepositoryMock
            .Setup(x => x.GetAsync(householdId, housemateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetAllHousemates(Guid householdId, List<Housemate> returns)
    {
        _housemateRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetAttendanceByDate(Guid householdId, DateOnly date, List<AttendanceRecord> returns)
    {
        _attendanceRepositoryMock
            .Setup(x => x.GetByDateAsync(householdId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetDish(Guid householdId, DateOnly date, DishRecord? returns)
    {
        _dishRepositoryMock
            .Setup(x => x.GetAsync(householdId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetCommentsByDate(Guid householdId, DateOnly date, List<Comment> returns)
    {
        _commentRepositoryMock
            .Setup(x => x.GetByDateAsync(householdId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetHistoryByDate(Guid householdId, DateOnly date, List<DayHistoryEntry> returns)
    {
        _dayHistoryRepositoryMock
            .Setup(x => x.GetByDateAsync(householdId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupDishUpsert()
    {
        _dishRepositoryMock
            .Setup(x => x.UpsertAsync(It.IsAny<DishRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupCommentUpsert()
    {
        _commentRepositoryMock
            .Setup(x => x.UpsertAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupCommentDelete()
    {
        _commentRepositoryMock
            .Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupDishDelete()
    {
        _dishRepositoryMock
            .Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupHistoryAdd()
    {
        _dayHistoryRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<DayHistoryEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupHistoryAddWithCapture(Action<DayHistoryEntry> capture)
    {
        _dayHistoryRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<DayHistoryEntry>(), It.IsAny<CancellationToken>()))
            .Callback<DayHistoryEntry, CancellationToken>((entry, _) => capture(entry))
            .Returns(Task.CompletedTask);
    }

    private void SetupGetAttendance(Guid householdId, DateOnly date, Guid housemateId, AttendanceRecord? returns)
    {
        _attendanceRepositoryMock
            .Setup(x => x.GetAsync(householdId, date, housemateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupAttendanceUpsert()
    {
        _attendanceRepositoryMock
            .Setup(x => x.UpsertAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupChefStatusUpsert()
    {
        _attendanceRepositoryMock
            .Setup(x => x.UpsertChefStatusAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static Housemate CreateHousemate(Guid householdId, Guid housemateId) =>
        new(housemateId, householdId, "Alice", HousemateColors.Palette[0], false);
}
