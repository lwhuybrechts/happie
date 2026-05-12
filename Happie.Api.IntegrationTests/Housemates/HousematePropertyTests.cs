using Azure.Data.Tables;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure;
using Happie.Api.Infrastructure.Mappers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.IntegrationTests.Infrastructure;
using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.IntegrationTests.Housemates;

/// <summary>Property-based tests for housemate management.</summary>
public class HousematePropertyTests
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly IHousemateRepository _housemateRepository;
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly HousemateHandler _sut;

    /// <summary>Initializes a new instance of <see cref="HousematePropertyTests"/> and truncates all relevant tables.</summary>
    public HousematePropertyTests()
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
            ?? "UseDevelopmentStorage=true";

        _tableServiceClient = new TableServiceClient(connectionString);

        TableHelper.TruncateTable(_tableServiceClient, "Housemates");
        TableHelper.TruncateTable(_tableServiceClient, "AttendanceRecords");
        TableHelper.TruncateTable(_tableServiceClient, "Comments");

        var storageClient = new TableStorageClient(_tableServiceClient);

        _housemateRepository = new HousemateRepository(storageClient, new HousemateMapper());
        _attendanceRepository = new AttendanceRepository(storageClient, new AttendanceRecordMapper());
        _commentRepository = new CommentRepository(storageClient, new CommentMapper());

        _sut = new HousemateHandler(_housemateRepository, _attendanceRepository, _commentRepository);
    }

    // Feature: happie, Property 22: Active housemate list contains no deleted housemates
    /// <summary>
    /// For any set of housemates where some are soft-deleted, <c>GetAllAsync</c> filtered to non-deleted
    /// must never return a housemate with <c>IsDeleted = true</c>.
    /// Validates: Requirements 12.1, 12.8
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetActiveHousematesAsync_WithDeletedHousemates_NeverReturnsDeleted()
    {
        return Prop.ForAll(
            HouseholdWithMixedHousematesArb(),
            async args =>
            {
                var (householdId, activeIds, deletedIds) = args;

                // Arrange.
                foreach (var (id, color) in activeIds)
                {
                    var housemate = new Housemate(id, householdId, "Active", color, false);
                    await _housemateRepository.UpsertAsync(housemate);
                }

                foreach (var (id, color) in deletedIds)
                {
                    var housemate = new Housemate(id, householdId, "Deleted", color, true);
                    await _housemateRepository.UpsertAsync(housemate);
                }

                // Act.
                var result = await _sut.GetActiveHousematesAsync(householdId);

                // Capture the set of deleted IDs before cleanup.
                var deletedIdSet = deletedIds.Select(x => x.Id).ToHashSet();

                // Clean up.
                foreach (var (id, _) in activeIds)
                    await _housemateRepository.DeleteAsync(householdId, id);

                foreach (var (id, _) in deletedIds)
                    await _housemateRepository.DeleteAsync(householdId, id);

                // Assert — none of the returned DTOs should belong to a soft-deleted housemate.
                return result.All(x => !deletedIdSet.Contains(x.Id))
                    .Label($"Active list must not contain deleted housemates for household {householdId}");
            });
    }

    // Feature: happie, Property 23: Add housemate round-trip
    /// <summary>
    /// For any valid name, adding a housemate and then fetching all housemates for that household
    /// must include the newly added housemate with the correct name.
    /// Validates: Requirements 12.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AddHousemateAsync_ValidName_AppearsInGetAll()
    {
        return Prop.ForAll(
            ValidHousemateNameArb(),
            async name =>
            {
                var householdId = Guid.NewGuid();

                // Act.
                var added = await _sut.AddHousemateAsync(householdId, name);
                var all = await _sut.GetActiveHousematesAsync(householdId);

                // Clean up.
                if (added is not null)
                    await _housemateRepository.DeleteAsync(householdId, added.Id);

                // Assert.
                return (added is not null && all.Any(x => x.Id == added.Id && x.Name == added.Name))
                    .Label($"Housemate with name '{name}' must appear in GetActiveHousematesAsync after being added");
            });
    }

    // Feature: happie, Property 27: Color uniqueness invariant within a household
    /// <summary>
    /// After adding multiple housemates to the same household, all active housemates must have distinct colors.
    /// Validates: Requirements 12.10, 12.11, 12.12, 12.13
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AddHousemateAsync_MultipleHousemates_AllColorsDistinct()
    {
        return Prop.ForAll(
            HousemateCountArb(),
            async count =>
            {
                var householdId = Guid.NewGuid();
                var addedIds = new List<Guid>();

                // Act.
                for (var i = 0; i < count; i++)
                {
                    var result = await _sut.AddHousemateAsync(householdId, $"Housemate{i}");
                    if (result is not null)
                        addedIds.Add(result.Id);
                }

                var all = await _sut.GetActiveHousematesAsync(householdId);

                // Clean up.
                foreach (var id in addedIds)
                    await _housemateRepository.DeleteAsync(householdId, id);

                // Assert.
                var colors = all.Select(x => x.Color).ToList();
                var distinctColors = colors.Distinct().ToList();

                return (colors.Count == distinctColors.Count)
                    .Label($"All {colors.Count} active housemates must have distinct colors in household {householdId}");
            });
    }

    // Feature: happie, Property 28: Rename round-trip
    /// <summary>
    /// For any valid new name, renaming a housemate and then fetching it must return the updated name.
    /// Validates: Requirements 12.14
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpdateHousemateAsync_ValidName_FetchReturnsUpdatedName()
    {
        return Prop.ForAll(
            ValidHousemateNameArb(),
            async newName =>
            {
                var householdId = Guid.NewGuid();
                var housemateId = Guid.NewGuid();

                // Arrange.
                var housemate = new Housemate(housemateId, householdId, "Original", HousemateColors.Palette[0], false);
                await _housemateRepository.UpsertAsync(housemate);

                // Act.
                await _sut.UpdateHousemateAsync(householdId, housemateId, newName, null);
                var fetched = await _housemateRepository.GetAsync(householdId, housemateId);

                // Clean up.
                await _housemateRepository.DeleteAsync(householdId, housemateId);

                // Assert.
                var expectedName = newName.Trim();

                return (fetched is not null && fetched.Name == expectedName)
                    .Label($"Housemate name must be '{expectedName}' after rename, but was '{fetched?.Name}'");
            });
    }

    // Feature: happie, Property 24: Hard delete removes housemate with no history
    /// <summary>
    /// For any housemate with no linked attendance records or comments, deleting it must result in it
    /// no longer appearing in <c>GetAllAsync</c>.
    /// Validates: Requirements 12.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeleteHousemateAsync_NoHistory_HousemateRemovedFromGetAll()
    {
        return Prop.ForAll(
            ValidHousemateNameArb(),
            async name =>
            {
                var householdId = Guid.NewGuid();
                var housemateId = Guid.NewGuid();

                // Arrange — add a housemate with no attendance or comments.
                var housemate = new Housemate(housemateId, householdId, name.Trim(), HousemateColors.Palette[0], false);
                await _housemateRepository.UpsertAsync(housemate);

                // Act.
                await _sut.DeleteHousemateAsync(householdId, housemateId);
                var all = await _housemateRepository.GetAllAsync(householdId);

                // Assert.
                return all.All(x => x.Id != housemateId)
                    .Label($"Hard-deleted housemate {housemateId} must not appear in GetAllAsync");
            });
    }

    /// <summary>Generates a household ID with two lists of (id, color) pairs: active and deleted housemates.</summary>
    private static Arbitrary<(Guid HouseholdId, List<(Guid Id, string Color)> Active, List<(Guid Id, string Color)> Deleted)> HouseholdWithMixedHousematesArb()
    {
        var guidGen = ArbMap.Default.GeneratorFor<Guid>();

        // Generate a count between 1 and 5 for active and 1 and 5 for deleted.
        var countGen = Gen.Choose(1, 5);

        var gen = guidGen.SelectMany(householdId =>
            countGen.SelectMany(activeCount =>
                countGen.SelectMany(deletedCount =>
                    // Generate enough GUIDs for all housemates; assign palette colors by index.
                    guidGen.ArrayOf(activeCount + deletedCount).Select(ids =>
                    {
                        var active = ids
                            .Take(activeCount)
                            .Select((id, i) => (id, HousemateColors.Palette[i]))
                            .ToList();

                        var deleted = ids
                            .Skip(activeCount)
                            .Select((id, i) => (id, HousemateColors.Palette[activeCount + i]))
                            .ToList();

                        return (HouseholdId: householdId, Active: active, Deleted: deleted);
                    }))));

        return Arb.From(gen);
    }

    /// <summary>Generates valid housemate names: 1–50 non-whitespace characters (trimmed).</summary>
    private static Arbitrary<string> ValidHousemateNameArb()
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

    /// <summary>Generates a count of housemates to add, between 1 and the palette size.</summary>
    private static Arbitrary<int> HousemateCountArb()
    {
        // Keep count small to stay within palette size and run quickly.
        var gen = Gen.Choose(1, 10);
        return Arb.From(gen);
    }
}
