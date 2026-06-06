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

namespace Happie.Api.IntegrationTests.Days;

/// <summary>Property-based tests for day plan operations.</summary>
public class DayPropertyTests
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly IHousemateRepository _housemateRepository;
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly IDishRepository _dishRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly IDayHistoryRepository _dayHistoryRepository;
    private readonly DayHandler _sut;

    /// <summary>Initializes a new instance of <see cref="DayPropertyTests"/> and truncates all relevant tables.</summary>
    public DayPropertyTests()
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
            ?? "UseDevelopmentStorage=true";

        _tableServiceClient = new TableServiceClient(connectionString);

        TableHelper.TruncateTable(_tableServiceClient, "Housemates");
        TableHelper.TruncateTable(_tableServiceClient, "AttendanceRecords");
        TableHelper.TruncateTable(_tableServiceClient, "DishRecords");
        TableHelper.TruncateTable(_tableServiceClient, "Comments");
        TableHelper.TruncateTable(_tableServiceClient, "DayHistory");

        var storageClient = new TableStorageClient(_tableServiceClient);

        _housemateRepository = new HousemateRepository(storageClient, new HousemateMapper());
        _attendanceRepository = new AttendanceRepository(storageClient, new AttendanceRecordMapper());
        _dishRepository = new DishRepository(storageClient, new DishRecordMapper());
        _commentRepository = new CommentRepository(storageClient, new CommentMapper());
        _dayHistoryRepository = new DayHistoryRepository(storageClient, new DayHistoryEntryMapper());

        _sut = new DayHandler(
            _housemateRepository,
            _attendanceRepository,
            _dishRepository,
            _commentRepository,
            _dayHistoryRepository,
            new NoOpPushHandler());
    }

    // Feature: happie, Property 10: Dish length validation
    /// <summary>
    /// For any dish description of at most 100 characters, saving it must succeed and the day plan must
    /// reflect the saved description. The validation boundary is enforced at the function layer (not handler),
    /// so this property tests that the handler correctly persists valid descriptions.
    /// Validates: Requirements 5.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertDishAsync_ValidLength_RoundTrips()
    {
        return Prop.ForAll(
            ValidDishDescriptionArb(),
            async description =>
            {
                var householdId = Guid.NewGuid();
                var housemateId = Guid.NewGuid();
                var date = new DateOnly(2025, 7, 15);

                // Arrange.
                var housemate = new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false);
                await _housemateRepository.UpsertAsync(housemate);

                // Act.
                await _sut.UpsertDishAsync(householdId, date, description, null, 0, housemateId);
                var fetched = await _dishRepository.GetAsync(householdId, date);

                // Clean up.
                await _housemateRepository.DeleteAsync(householdId, housemateId);

                // Assert.
                return (fetched is not null && fetched.Description == description)
                    .Label($"Expected dish description '{description}' but got '{fetched?.Description}'");
            });
    }

    // Feature: happie, Property 11: Comment slot — one per housemate per day
    /// <summary>
    /// For any housemate and date, saving two different comments in sequence must result in exactly one
    /// comment being stored, containing the text of the second save.
    /// Validates: Requirements 6.1, 6.2, 6.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertCommentAsync_TwiceForSameHousemate_OnlyLatestCommentStored()
    {
        return Prop.ForAll(
            TwoDistinctCommentTextsArb(),
            async pair =>
            {
                var (firstText, secondText) = pair;
                var householdId = Guid.NewGuid();
                var housemateId = Guid.NewGuid();
                var date = new DateOnly(2025, 7, 15);

                // Arrange.
                var housemate = new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false);
                await _housemateRepository.UpsertAsync(housemate);

                // Act.
                await _sut.UpsertCommentAsync(householdId, date, housemateId, firstText, housemateId);
                await _sut.UpsertCommentAsync(householdId, date, housemateId, secondText, housemateId);

                var dayPlan = await _sut.GetDayPlanAsync(householdId, date);

                // Clean up.
                await _commentRepository.DeleteAsync(householdId, date, housemateId);
                await _housemateRepository.DeleteAsync(householdId, housemateId);

                // Assert — exactly one comment for this housemate, containing the second text.
                var commentsForHousemate = dayPlan.Comments.Where(x => x.HousemateId == housemateId).ToList();

                return (commentsForHousemate.Count == 1 && commentsForHousemate[0].Text == secondText)
                    .Label($"Expected exactly one comment with text '{secondText}', but got {commentsForHousemate.Count} comment(s)");
            });
    }

    // Feature: happie, Property 12: Comment deletion removes the comment
    /// <summary>
    /// For any housemate and date where a comment exists, deleting the comment and then retrieving the
    /// day plan must return no comment for that housemate on that day.
    /// Validates: Requirements 6.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeleteCommentAsync_AfterUpsert_CommentAbsentFromDayPlan()
    {
        return Prop.ForAll(
            ValidCommentTextArb(),
            async text =>
            {
                var householdId = Guid.NewGuid();
                var housemateId = Guid.NewGuid();
                var date = new DateOnly(2025, 7, 15);

                // Arrange.
                var housemate = new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false);
                await _housemateRepository.UpsertAsync(housemate);

                // Act.
                await _sut.UpsertCommentAsync(householdId, date, housemateId, text, housemateId);
                await _sut.DeleteCommentAsync(householdId, date, housemateId, housemateId);

                var dayPlan = await _sut.GetDayPlanAsync(householdId, date);

                // Clean up.
                await _housemateRepository.DeleteAsync(householdId, housemateId);

                // Assert.
                var commentsForHousemate = dayPlan.Comments.Where(x => x.HousemateId == housemateId).ToList();

                return (commentsForHousemate.Count == 0)
                    .Label($"Expected no comments for housemate {housemateId} after deletion, but found {commentsForHousemate.Count}");
            });
    }

    // Feature: happie, Property 13: Comment length validation
    /// <summary>
    /// For any comment text of at most 200 characters, saving it must succeed and the day plan must
    /// reflect the saved text. The 200-char limit is enforced at the function layer; this property
    /// tests that the handler correctly persists valid texts.
    /// Validates: Requirements 6.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpsertCommentAsync_ValidLength_RoundTrips()
    {
        return Prop.ForAll(
            ValidCommentTextArb(),
            async text =>
            {
                var householdId = Guid.NewGuid();
                var housemateId = Guid.NewGuid();
                var date = new DateOnly(2025, 7, 15);

                // Arrange.
                var housemate = new Housemate(housemateId, householdId, "Alice", HousemateColors.Palette[0], false);
                await _housemateRepository.UpsertAsync(housemate);

                // Act.
                await _sut.UpsertCommentAsync(householdId, date, housemateId, text, housemateId);
                var fetched = await _commentRepository.GetAsync(householdId, date, housemateId);

                // Clean up.
                await _commentRepository.DeleteAsync(householdId, date, housemateId);
                await _housemateRepository.DeleteAsync(householdId, housemateId);

                // Assert.
                return (fetched is not null && fetched.Text == text)
                    .Label($"Expected comment text '{text}' but got '{fetched?.Text}'");
            });
    }

    // Feature: happie, Property 29: Calendar color indicators match eating-in housemates
    /// <summary>
    /// For any set of housemates with mixed attendance statuses on a given day, the calendar response
    /// for that day must contain exactly the colors of housemates with EatingIn status — no more, no less.
    /// Validates: Requirements 13.2, 13.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetCalendarAsync_EatingInColors_MatchEatingInHousemates()
    {
        return Prop.ForAll(
            HousematesWithAttendanceArb(),
            async args =>
            {
                var (householdId, housemates, attendanceStatuses) = args;
                var date = new DateOnly(2025, 7, 15);

                // Arrange — upsert housemates and their attendance records.
                foreach (var housemate in housemates)
                    await _housemateRepository.UpsertAsync(housemate);

                for (var i = 0; i < housemates.Count; i++)
                {
                    var record = new AttendanceRecord(householdId, housemates[i].Id, date, attendanceStatuses[i], false, null);
                    await _attendanceRepository.UpsertAsync(record);
                }

                // Act.
                var calendar = await _sut.GetCalendarAsync(householdId, date, date);
                var dayEntry = calendar.Days.FirstOrDefault(x => x.Date == date);

                // Determine expected colors: only EatingIn housemates.
                var expectedColors = housemates
                    .Where((x, i) => attendanceStatuses[i] == AttendanceStatus.EatingIn)
                    .Select(x => x.Color)
                    .OrderBy(x => x)
                    .ToList();

                var actualColors = (dayEntry?.EatingInColors ?? Array.Empty<string>())
                    .OrderBy(x => x)
                    .ToList();

                // Clean up.
                foreach (var housemate in housemates)
                {
                    await _attendanceRepository.UpsertAsync(new AttendanceRecord(householdId, housemate.Id, date, AttendanceStatus.Unknown, false, null));
                    await _housemateRepository.DeleteAsync(householdId, housemate.Id);
                }

                // Assert.
                return expectedColors.SequenceEqual(actualColors)
                    .Label($"Expected colors [{string.Join(", ", expectedColors)}] but got [{string.Join(", ", actualColors)}]");
            });
    }

    /// <summary>Generates valid dish descriptions: 1–100 non-empty characters.</summary>
    private static Arbitrary<string> ValidDishDescriptionArb()
    {
        var gen = Gen.Choose(1, 100)
            .SelectMany(len =>
                Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                             'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                             'u', 'v', 'w', 'x', 'y', 'z')
                    .ArrayOf(len)
                    .Select(chars => new string(chars)));

        return Arb.From(gen);
    }

    /// <summary>Generates valid comment texts: 1–200 non-empty characters.</summary>
    private static Arbitrary<string> ValidCommentTextArb()
    {
        var gen = Gen.Choose(1, 200)
            .SelectMany(len =>
                Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                             'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                             'u', 'v', 'w', 'x', 'y', 'z')
                    .ArrayOf(len)
                    .Select(chars => new string(chars)));

        return Arb.From(gen);
    }

    /// <summary>Generates two distinct comment texts of valid length.</summary>
    private static Arbitrary<(string First, string Second)> TwoDistinctCommentTextsArb()
    {
        var textGen = Gen.Choose(1, 50)
            .SelectMany(len =>
                Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                             'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                             'u', 'v', 'w', 'x', 'y', 'z')
                    .ArrayOf(len)
                    .Select(chars => new string(chars)));

        var gen = textGen.SelectMany(first =>
            textGen
                .Where(second => second != first)
                .Select(second => (First: first, Second: second)));

        return Arb.From(gen);
    }

    /// <summary>
    /// Generates a household ID, a list of 1–5 active housemates with distinct palette colors,
    /// and a matching list of attendance statuses.
    /// </summary>
    private static Arbitrary<(Guid HouseholdId, List<Housemate> Housemates, List<AttendanceStatus> Statuses)> HousematesWithAttendanceArb()
    {
        var guidGen = ArbMap.Default.GeneratorFor<Guid>();
        var statusGen = Gen.Elements(AttendanceStatus.Unknown, AttendanceStatus.EatingIn, AttendanceStatus.NotEatingIn);

        var gen = guidGen.SelectMany(householdId =>
            Gen.Choose(1, 5).SelectMany(count =>
                guidGen.ArrayOf(count).SelectMany(ids =>
                    statusGen.ArrayOf(count).Select(statuses =>
                    {
                        var housemates = ids
                            .Select((id, i) => new Housemate(id, householdId, $"Housemate{i}", HousemateColors.Palette[i], false))
                            .ToList();

                        return (HouseholdId: householdId, Housemates: housemates, Statuses: statuses.ToList());
                    }))));

        return Arb.From(gen);
    }
}
