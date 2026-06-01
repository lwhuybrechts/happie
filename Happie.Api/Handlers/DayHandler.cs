using Happie.Api.Infrastructure.Repositories;
using Happie.Shared.Contracts;
using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.Handlers;

/// <summary>Handles day plan operations.</summary>
public class DayHandler : IDayHandler
{
    private readonly IHousemateRepository _housemateRepository;
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly IDishRepository _dishRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly IDayHistoryRepository _dayHistoryRepository;
    private readonly IPushHandler _pushHandler;

    /// <summary>Initializes a new instance of <see cref="DayHandler"/>.</summary>
    public DayHandler(
        IHousemateRepository housemateRepository,
        IAttendanceRepository attendanceRepository,
        IDishRepository dishRepository,
        ICommentRepository commentRepository,
        IDayHistoryRepository dayHistoryRepository,
        IPushHandler pushHandler)
    {
        _housemateRepository = housemateRepository;
        _attendanceRepository = attendanceRepository;
        _dishRepository = dishRepository;
        _commentRepository = commentRepository;
        _dayHistoryRepository = dayHistoryRepository;
        _pushHandler = pushHandler;
    }

    /// <inheritdoc/>
    public async Task<DayPlanResponse> GetDayPlanAsync(Guid householdId, DateOnly date, CancellationToken ct = default)
    {
        // Fetch all data in parallel.
        var housematesTask = _housemateRepository.GetAllAsync(householdId, ct);
        var attendanceTask = _attendanceRepository.GetByDateAsync(householdId, date, ct);
        var dishTask = _dishRepository.GetAsync(householdId, date, ct);
        var commentsTask = _commentRepository.GetByDateAsync(householdId, date, ct);
        var historyTask = _dayHistoryRepository.GetByDateAsync(householdId, date, ct);

        await Task.WhenAll(housematesTask, attendanceTask, dishTask, commentsTask, historyTask);

        var allHousemates = await housematesTask;
        var attendanceRecords = await attendanceTask;
        var dish = await dishTask;
        var comments = await commentsTask;
        var historyEntries = await historyTask;

        // Build a lookup of housemate ID → housemate for efficient access.
        var housemateById = allHousemates.ToDictionary(x => x.Id);

        // Build attendance entries for all active housemates, defaulting to Unknown.
        var attendanceByHousemateId = attendanceRecords.ToDictionary(x => x.HousemateId);

        var attendance = allHousemates
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x =>
            {
                var status = attendanceByHousemateId.TryGetValue(x.Id, out var record)
                    ? record.Status
                    : AttendanceStatus.Unknown;
                var isChef = attendanceByHousemateId.TryGetValue(x.Id, out var chefRecord)
                    ? chefRecord.IsChef
                    : false;
                return new AttendanceDto(x.Id, x.Name, x.Color, status, isChef);
            })
            .ToList();

        // Build dish DTO.
        var DishDto = dish is null
            ? null
            : new DishDto(dish.Description, dish.LastChangedByHousemateId, dish.LastChangedAt);

        // Build comment DTOs — include only housemates who have a comment.
        // Soft-deleted housemates are included if they have a comment; their name is formatted as "Name (deleted)".
        var commentDtos = comments
            .Select(x =>
            {
                var name = ResolveHousemateName(housemateById, x.HousemateId);
                var color = housemateById.TryGetValue(x.HousemateId, out var housemate) ? housemate.Color : string.Empty;
                return new CommentDto(x.HousemateId, name, color, x.Text, x.LastEditedAt);
            })
            .ToList();

        // Build history entry DTOs — already in reverse-chronological order from the repository.
        var historyDtos = historyEntries
            .Select(x =>
            {
                var name = ResolveHousemateName(housemateById, x.ChangedByHousemateId);
                return new HistoryEntryDto(x.ChangedAt, x.ChangedByHousemateId, name, x.ChangeType, x.Description);
            })
            .ToList();

        return new DayPlanResponse(date, DishDto, attendance, commentDtos, historyDtos);
    }

    /// <inheritdoc/>
    public async Task<bool> UpsertAttendanceAsync(Guid householdId, DateOnly date, Guid housemateId, AttendanceStatus status, Guid actingHousemateId, CancellationToken ct = default)
    {
        var housemate = await _housemateRepository.GetAsync(householdId, housemateId, ct);
        if (housemate is null)
            return false;

        // Read existing record to preserve IsChef value (default to false if no record exists).
        var existingRecord = await _attendanceRepository.GetAsync(householdId, date, housemateId, ct);
        var isChef = existingRecord?.IsChef ?? false;

        var record = new AttendanceRecord(householdId, housemateId, date, status, isChef);
        var historyEntry = new DayHistoryEntry(
            householdId,
            date,
            DateTimeOffset.UtcNow,
            actingHousemateId,
            ChangeType.Attendance,
            $"{housemate.Name}'s attendance set to {status}.");

        await Task.WhenAll(
            _attendanceRepository.UpsertAsync(record, ct),
            _dayHistoryRepository.AddAsync(historyEntry, ct));

        // Send auto-notifications for today and tomorrow only; failures must not interrupt the save.
        if (IsTodayOrTomorrow(date))
            await _pushHandler.SendAutoNotificationsAsync(householdId, actingHousemateId, date, historyEntry.Description, ct);

        return true;
    }

    /// <inheritdoc/>
    public async Task UpsertDishAsync(Guid householdId, DateOnly date, string description, Guid actingHousemateId, CancellationToken ct = default)
    {
        var record = new DishRecord(householdId, date, description, actingHousemateId, DateTimeOffset.UtcNow);
        var historyEntry = new DayHistoryEntry(
            householdId,
            date,
            DateTimeOffset.UtcNow,
            actingHousemateId,
            ChangeType.Dish,
            $"Dish set to \"{description}\".");

        await Task.WhenAll(
            _dishRepository.UpsertAsync(record, ct),
            _dayHistoryRepository.AddAsync(historyEntry, ct));

        // Send auto-notifications for today and tomorrow only; failures must not interrupt the save.
        if (IsTodayOrTomorrow(date))
            await _pushHandler.SendAutoNotificationsAsync(householdId, actingHousemateId, date, historyEntry.Description, ct);
    }

    /// <inheritdoc/>
    public async Task<bool> UpsertCommentAsync(Guid householdId, DateOnly date, Guid housemateId, string text, Guid actingHousemateId, CancellationToken ct = default)
    {
        var housemate = await _housemateRepository.GetAsync(householdId, housemateId, ct);
        if (housemate is null)
            return false;

        var comment = new Comment(householdId, housemateId, date, text, DateTimeOffset.UtcNow);
        var historyEntry = new DayHistoryEntry(
            householdId,
            date,
            DateTimeOffset.UtcNow,
            actingHousemateId,
            ChangeType.Comment,
            $"{housemate.Name}'s comment set to \"{text}\".");

        await Task.WhenAll(
            _commentRepository.UpsertAsync(comment, ct),
            _dayHistoryRepository.AddAsync(historyEntry, ct));

        // Send auto-notifications for today and tomorrow only; failures must not interrupt the save.
        if (IsTodayOrTomorrow(date))
            await _pushHandler.SendAutoNotificationsAsync(householdId, actingHousemateId, date, historyEntry.Description, ct);

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteCommentAsync(Guid householdId, DateOnly date, Guid housemateId, Guid actingHousemateId, CancellationToken ct = default)
    {
        var housemate = await _housemateRepository.GetAsync(householdId, housemateId, ct);
        if (housemate is null)
            return false;

        var historyEntry = new DayHistoryEntry(
            householdId,
            date,
            DateTimeOffset.UtcNow,
            actingHousemateId,
            ChangeType.Comment,
            $"{housemate.Name}'s comment was deleted.");

        await Task.WhenAll(
            _commentRepository.DeleteAsync(householdId, date, housemateId, ct),
            _dayHistoryRepository.AddAsync(historyEntry, ct));

        // Send auto-notifications for today and tomorrow only; failures must not interrupt the save.
        if (IsTodayOrTomorrow(date))
            await _pushHandler.SendAutoNotificationsAsync(householdId, actingHousemateId, date, historyEntry.Description, ct);

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> UpsertChefStatusAsync(Guid householdId, DateOnly date, Guid housemateId, bool isChef, Guid actingHousemateId, CancellationToken cancellationToken = default)
    {
        var housemate = await _housemateRepository.GetAsync(householdId, housemateId, cancellationToken);
        if (housemate is null || housemate.IsDeleted)
            return false;

        var statusDescription = isChef ? "enabled" : "disabled";
        var historyEntry = new DayHistoryEntry(
            householdId,
            date,
            DateTimeOffset.UtcNow,
            actingHousemateId,
            ChangeType.ChefStatusChanged,
            $"{housemate.Name}'s chef status {statusDescription}.");

        await Task.WhenAll(
            _attendanceRepository.UpsertChefStatusAsync(householdId, date, housemateId, isChef, cancellationToken),
            _dayHistoryRepository.AddAsync(historyEntry, cancellationToken));

        return true;
    }

    /// <summary>Returns true when the given date is today or tomorrow (UTC).</summary>
    private static bool IsTodayOrTomorrow(DateOnly date)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return date == today || date == today.AddDays(1);
    }

    /// <inheritdoc/>
    public async Task<CalendarResponse> GetCalendarAsync(Guid householdId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var housematesTask = _housemateRepository.GetAllAsync(householdId, ct);
        var attendanceTask = _attendanceRepository.GetByDateRangeAsync(householdId, from, to, ct);

        await Task.WhenAll(housematesTask, attendanceTask);

        var housemates = await housematesTask;
        var attendanceRecords = await attendanceTask;

        // Build a lookup of housemate ID → color for active (non-deleted) housemates only.
        var colorById = housemates
            .Where(x => !x.IsDeleted)
            .ToDictionary(x => x.Id, x => x.Color);

        // Group attendance records by date and collect EatingIn colors.
        var byDate = attendanceRecords
            .Where(x => x.Status == AttendanceStatus.EatingIn && colorById.ContainsKey(x.HousemateId))
            .GroupBy(x => x.Date)
            .ToDictionary(x => x.Key, x => x.Select(r => colorById[r.HousemateId]).ToList());

        // Enumerate every date in the range, including days with no EatingIn records.
        var days = new List<CalendarDayDto>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var colors = byDate.TryGetValue(d, out var list)
                ? (IReadOnlyList<string>)list
                : Array.Empty<string>();
            days.Add(new CalendarDayDto(d, colors));
        }

        return new CalendarResponse(days);
    }

    /// <summary>
    /// Resolves the display name for a housemate.
    /// Soft-deleted housemates are formatted as "Name (deleted)".
    /// Unknown housemates (hard-deleted or missing) fall back to an empty string.
    /// </summary>
    private static string ResolveHousemateName(Dictionary<Guid, Housemate> housemateById, Guid housemateId)
    {
        if (!housemateById.TryGetValue(housemateId, out var housemate))
            return string.Empty;

        return housemate.IsDeleted ? $"{housemate.Name} (deleted)" : housemate.Name;
    }
}
