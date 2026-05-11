using Azure.Data.Tables;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Infrastructure;
using Happie.Api.Repositories;
using Happie.Api.Repositories.Mappers;
using Happie.Shared.Domain;

namespace Happie.Tests;

/// <summary>Property-based tests verifying data isolation between households.</summary>
public class DataIsolationTests
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly IHousemateRepository _housemateRepository;
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly IDishRepository _dishRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly IPushSubscriptionRepository _pushSubscriptionRepository;
    private readonly IDayHistoryRepository _dayHistoryRepository;

    /// <summary>Initializes a new instance of <see cref="DataIsolationTests"/> and truncates all relevant tables.</summary>
    public DataIsolationTests()
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
            ?? "UseDevelopmentStorage=true";

        _tableServiceClient = new TableServiceClient(connectionString);

        // Truncate all tables before each test to ensure a clean state.
        TableHelper.TruncateTable(_tableServiceClient, "Housemates");
        TableHelper.TruncateTable(_tableServiceClient, "AttendanceRecords");
        TableHelper.TruncateTable(_tableServiceClient, "DishRecords");
        TableHelper.TruncateTable(_tableServiceClient, "Comments");
        TableHelper.TruncateTable(_tableServiceClient, "PushSubscriptions");
        TableHelper.TruncateTable(_tableServiceClient, "DayHistory");

        var storageClient = new TableStorageClient(_tableServiceClient);

        _housemateRepository = new HousemateRepository(storageClient, new HousemateMapper());
        _attendanceRepository = new AttendanceRepository(storageClient, new AttendanceRecordMapper());
        _dishRepository = new DishRepository(storageClient, new DishRecordMapper());
        _commentRepository = new CommentRepository(storageClient, new CommentMapper());
        _pushSubscriptionRepository = new PushSubscriptionRepository(storageClient, new PushSubscriptionMapper());
        _dayHistoryRepository = new DayHistoryRepository(storageClient, new DayHistoryEntryMapper());
    }

    // Feature: happie, Property 6: Data isolation between households
    /// <summary>
    /// For any two distinct household IDs A and B, housemates written under household A must not appear
    /// in queries scoped to household B.
    /// Validates: Requirements 1.8, 2.2, 2.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Task<Property> HousemateRepository_DataIsolation_BetweenHouseholds()
    {
        return Prop.ForAll(
            DistinctHouseholdPairArb(),
            async pair =>
            {
                var (householdA, householdB) = pair;
                var housemateId = Guid.NewGuid();

                // Write a housemate under household A.
                var housemate = new Housemate(housemateId, householdA, "Alice", HousemateColors.Palette[0], false);
                await _housemateRepository.UpsertAsync(housemate);

                // Query from household B — must return no results belonging to household A.
                var resultsB = await _housemateRepository.GetAllAsync(householdB);

                // Clean up to avoid cross-iteration interference when household GUIDs collide.
                await _housemateRepository.DeleteAsync(householdA, housemateId);

                return resultsB.All(h => h.HouseholdId != householdA)
                    .Label($"Housemate written to household A ({householdA}) must not appear in household B ({householdB})");
            }).ToProperty();
    }

    // Feature: happie, Property 6: Data isolation between households
    /// <summary>
    /// For any two distinct household IDs A and B, attendance records written under household A must not appear
    /// in queries scoped to household B.
    /// Validates: Requirements 1.8, 2.2, 2.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Task<Property> AttendanceRepository_DataIsolation_BetweenHouseholds()
    {
        return Prop.ForAll(
            DistinctHouseholdPairWithDateArb(),
            async args =>
            {
                var (householdA, householdB, housemateId, date) = args;

                // Write an attendance record under household A.
                var record = new AttendanceRecord(householdA, housemateId, date, AttendanceStatus.EatingIn);
                await _attendanceRepository.UpsertAsync(record);

                // Query from household B on the same date — must return no results belonging to household A.
                var resultsB = await _attendanceRepository.GetByDateAsync(householdB, date);

                return resultsB.All(r => r.HouseholdId != householdA)
                    .Label($"Attendance written to household A ({householdA}) must not appear in household B ({householdB})");
            }).ToProperty();
    }

    // Feature: happie, Property 6: Data isolation between households
    /// <summary>
    /// For any two distinct household IDs A and B, dish records written under household A must not appear
    /// in queries scoped to household B.
    /// Validates: Requirements 1.8, 2.2, 2.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Task<Property> DishRepository_DataIsolation_BetweenHouseholds()
    {
        return Prop.ForAll(
            DistinctHouseholdPairWithDateArb(),
            async args =>
            {
                var (householdA, householdB, housemateId, date) = args;

                // Write a dish record under household A.
                var dish = new DishRecord(householdA, date, "Pasta");
                await _dishRepository.UpsertAsync(dish, housemateId);

                // Query from household B on the same date — must return null (no dish for household B).
                var resultB = await _dishRepository.GetAsync(householdB, date);

                return (resultB == null || resultB.HouseholdId != householdA)
                    .Label($"Dish written to household A ({householdA}) must not appear in household B ({householdB})");
            }).ToProperty();
    }

    // Feature: happie, Property 6: Data isolation between households
    /// <summary>
    /// For any two distinct household IDs A and B, comments written under household A must not appear
    /// in queries scoped to household B.
    /// Validates: Requirements 1.8, 2.2, 2.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Task<Property> CommentRepository_DataIsolation_BetweenHouseholds()
    {
        return Prop.ForAll(
            DistinctHouseholdPairWithDateArb(),
            async args =>
            {
                var (householdA, householdB, housemateId, date) = args;

                // Write a comment under household A.
                var comment = new Comment(householdA, housemateId, date, "Home late");
                await _commentRepository.UpsertAsync(comment);

                // Query from household B on the same date — must return no results belonging to household A.
                var resultsB = await _commentRepository.GetByDateAsync(householdB, date);

                // Clean up to avoid cross-iteration interference when household GUIDs collide.
                await _commentRepository.DeleteAsync(householdA, date, housemateId);

                return resultsB.All(c => c.HouseholdId != householdA)
                    .Label($"Comment written to household A ({householdA}) must not appear in household B ({householdB})");
            }).ToProperty();
    }

    // Feature: happie, Property 6: Data isolation between households
    /// <summary>
    /// For any two distinct household IDs A and B, push subscriptions written under household A must not appear
    /// in queries scoped to household B.
    /// Validates: Requirements 1.8, 2.2, 2.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Task<Property> PushSubscriptionRepository_DataIsolation_BetweenHouseholds()
    {
        return Prop.ForAll(
            DistinctHouseholdPairArb(),
            async pair =>
            {
                var (householdA, householdB) = pair;
                var housemateId = Guid.NewGuid();

                // Write a push subscription under household A.
                var subscription = new PushSubscription(
                    housemateId,
                    householdA,
                    "https://push.example.com/endpoint",
                    "p256dhKey",
                    "authKey",
                    Locale.En);
                await _pushSubscriptionRepository.UpsertAsync(subscription);

                // Query from household B — must return no results belonging to household A.
                var resultsB = await _pushSubscriptionRepository.GetAllAsync(householdB);

                // Clean up to avoid cross-iteration interference when household GUIDs collide.
                await _pushSubscriptionRepository.DeleteAsync(householdA, housemateId);

                return resultsB.All(s => s.HouseholdId != householdA)
                    .Label($"Push subscription written to household A ({householdA}) must not appear in household B ({householdB})");
            }).ToProperty();
    }

    // Feature: happie, Property 6: Data isolation between households
    /// <summary>
    /// For any two distinct household IDs A and B, day history entries written under household A must not appear
    /// in queries scoped to household B.
    /// Validates: Requirements 1.8, 2.2, 2.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Task<Property> DayHistoryRepository_DataIsolation_BetweenHouseholds()
    {
        return Prop.ForAll(
            DistinctHouseholdPairWithDateArb(),
            async args =>
            {
                var (householdA, householdB, housemateId, date) = args;

                // Write a day history entry under household A.
                var entry = new DayHistoryEntry(
                    householdA,
                    date,
                    DateTimeOffset.UtcNow,
                    housemateId,
                    ChangeType.Attendance,
                    "Set attendance to EatingIn");
                await _dayHistoryRepository.AddAsync(entry);

                // Query from household B on the same date — must return no results belonging to household A.
                var resultsB = await _dayHistoryRepository.GetByDateAsync(householdB, date);

                return resultsB.All(e => e.HouseholdId != householdA)
                    .Label($"Day history written to household A ({householdA}) must not appear in household B ({householdB})");
            }).ToProperty();
    }

    /// <summary>Generates two distinct household GUIDs as a tuple.</summary>
    private static Arbitrary<(Guid HouseholdA, Guid HouseholdB)> DistinctHouseholdPairArb()
    {
        var guidGen = ArbMap.Default.GeneratorFor<Guid>();
        var pairGen = guidGen.SelectMany(a => guidGen
            .Where(b => b != a)
            .Select(b => (HouseholdA: a, HouseholdB: b)));
        return Arb.From(pairGen);
    }

    /// <summary>Generates two distinct household GUIDs, a housemate GUID, and a date as a tuple.</summary>
    private static Arbitrary<(Guid HouseholdA, Guid HouseholdB, Guid HousemateId, DateOnly Date)> DistinctHouseholdPairWithDateArb()
    {
        var guidGen = ArbMap.Default.GeneratorFor<Guid>();
        var minDay = new DateOnly(2020, 1, 1).DayNumber;
        var maxDay = new DateOnly(2030, 12, 31).DayNumber;
        var dateGen = Gen.Choose(minDay, maxDay).Select(dayNumber => DateOnly.FromDayNumber(dayNumber));

        var gen = guidGen.SelectMany(a => guidGen
            .Where(b => b != a)
            .SelectMany(b => guidGen
                .SelectMany(housemateId => dateGen
                    .Select(date => (HouseholdA: a, HouseholdB: b, HousemateId: housemateId, Date: date)))));

        return Arb.From(gen);
    }
}
