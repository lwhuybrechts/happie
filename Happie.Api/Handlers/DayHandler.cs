using System.Text.Json;
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
            : new DishDto(dish.Description, dish.LastChangedByHousemateId, dish.LastChangedAt, dish.DinnerTime?.Hour, dish.DinnerTime?.Minute);

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
        // Resolve housemate IDs in the parameters JSON to current names before sending to the client.
        var historyDtos = historyEntries
            .Select(x =>
            {
                var name = ResolveHousemateName(housemateById, x.ChangedByHousemateId);
                var resolvedParameters = ParameterNameResolver.Resolve(x.Parameters, housemateById);
                return new HistoryEntryDto(x.ChangedAt, x.ChangedByHousemateId, name, x.ChangeType, x.TranslationKey, resolvedParameters);
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

        var record = new AttendanceRecord(householdId, housemateId, date, status, isChef, DateTimeOffset.UtcNow);
        var parameters = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["name"] = housemateId.ToString(),
            ["status"] = status.ToString()
        });
        var historyEntry = new DayHistoryEntry(
            householdId,
            date,
            DateTimeOffset.UtcNow,
            actingHousemateId,
            ChangeType.Attendance,
            TranslationKeys.HistoryAttendanceSet,
            parameters);

        await Task.WhenAll(
            _attendanceRepository.UpsertAsync(record, ct),
            _dayHistoryRepository.AddAsync(historyEntry, ct));

        // Send auto-notifications for today and tomorrow only; failures must not interrupt the save.
        if (IsTodayOrTomorrow(date))
            await _pushHandler.SendAutoNotificationsAsync(householdId, actingHousemateId, date, historyEntry.TranslationKey, historyEntry.Parameters, ct);

        return true;
    }

    /// <inheritdoc/>
    public async Task UpsertDishAsync(Guid householdId, DateOnly date, string description, TimeOnly? dinnerTime, int timezoneOffsetMinutes, Guid actingHousemateId, CancellationToken ct = default)
    {
        // Fetch existing record to compare old values.
        var existingDish = await _dishRepository.GetAsync(householdId, date, ct);

        var dishChanged = existingDish is null || existingDish.Description != description;
        var dinnerTimeChanged = existingDish?.DinnerTime != dinnerTime;

        var record = new DishRecord(householdId, date, description, actingHousemateId, DateTimeOffset.UtcNow, dinnerTime, DateTimeOffset.UtcNow);
        await _dishRepository.UpsertAsync(record, ct);

        // Write a single history entry based on what changed.
        if (dishChanged || dinnerTimeChanged)
        {
            var historyEntry = CreateDishHistoryEntry(householdId, date, actingHousemateId, description, dinnerTime, dishChanged, dinnerTimeChanged);

            try
            {
                await _dayHistoryRepository.AddAsync(historyEntry, ct);
            }
            catch (Exception)
            {
                // History entry write failure must not roll back the dish save (Requirement 8.8).
            }
        }

        // Consolidated push notification: at most one per save.
        var dinnerTimeCleared = dinnerTimeChanged && dinnerTime is null;
        var shouldNotifyDish = dishChanged && IsTodayOrTomorrow(date);
        var shouldNotifyDinnerTime = dinnerTimeChanged && !dinnerTimeCleared
            && IsDinnerTimeWithinWindow(date, dinnerTime!.Value, timezoneOffsetMinutes);

        if (shouldNotifyDish || shouldNotifyDinnerTime)
        {
            var (notificationKey, notificationParameters) = GetNotificationKeyAndParameters(description, dinnerTime, shouldNotifyDish, shouldNotifyDinnerTime);
            await _pushHandler.SendAutoNotificationsAsync(householdId, actingHousemateId, date, notificationKey, notificationParameters, ct);
        }
    }

    /// <summary>Creates the appropriate history entry based on what changed in a dish save.</summary>
    private static DayHistoryEntry CreateDishHistoryEntry(
        Guid householdId,
        DateOnly date,
        Guid actingHousemateId,
        string description,
        TimeOnly? dinnerTime,
        bool dishChanged,
        bool dinnerTimeChanged)
    {
        ChangeType changeType;
        string translationKey;
        Dictionary<string, string> parameterDict;

        if (dishChanged && dinnerTimeChanged)
        {
            // Both changed.
            changeType = ChangeType.DishAndDinnerTime;
            if (dinnerTime.HasValue)
            {
                translationKey = TranslationKeys.HistoryDishAndDinnerTimeSet;
                parameterDict = new Dictionary<string, string>
                {
                    ["description"] = description,
                    ["time"] = dinnerTime.Value.ToString("HH:mm")
                };
            }
            else
            {
                translationKey = TranslationKeys.HistoryDishSetDinnerTimeCleared;
                parameterDict = new Dictionary<string, string>
                {
                    ["description"] = description
                };
            }
        }
        else if (dinnerTimeChanged)
        {
            // Only dinner time changed.
            changeType = ChangeType.DinnerTime;
            if (dinnerTime.HasValue)
            {
                translationKey = TranslationKeys.HistoryDinnerTimeSet;
                parameterDict = new Dictionary<string, string>
                {
                    ["time"] = dinnerTime.Value.ToString("HH:mm")
                };
            }
            else
            {
                translationKey = TranslationKeys.HistoryDinnerTimeCleared;
                parameterDict = new Dictionary<string, string>();
            }
        }
        else
        {
            // Only dish changed.
            changeType = ChangeType.Dish;
            translationKey = TranslationKeys.HistoryDishSet;
            parameterDict = new Dictionary<string, string>
            {
                ["description"] = description
            };
        }

        var parameters = JsonSerializer.Serialize(parameterDict);
        return new DayHistoryEntry(
            householdId,
            date,
            DateTimeOffset.UtcNow,
            actingHousemateId,
            changeType,
            translationKey,
            parameters);
    }

    /// <summary>
    /// Returns true when the new dinner time is less than 6 hours away from the setter's local time.
    /// The setter's local time is computed as UTC now + the client-provided timezone offset.
    /// </summary>
    private static bool IsDinnerTimeWithinWindow(DateOnly date, TimeOnly dinnerTime, int timezoneOffsetMinutes)
    {
        return IsDinnerTimeWithinWindow(date, dinnerTime, timezoneOffsetMinutes, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Testable overload that accepts the current UTC time as a parameter.
    /// Returns true when the new dinner time is less than 6 hours away from the setter's local time.
    /// </summary>
    internal static bool IsDinnerTimeWithinWindow(DateOnly date, TimeOnly dinnerTime, int timezoneOffsetMinutes, DateTimeOffset currentUtcTime)
    {
        var setterLocalNow = currentUtcTime.AddMinutes(timezoneOffsetMinutes);
        var todayAtDinnerTime = new DateTime(date.Year, date.Month, date.Day, dinnerTime.Hour, dinnerTime.Minute, 0);
        var difference = todayAtDinnerTime - setterLocalNow.DateTime;

        return difference > TimeSpan.Zero && difference < TimeSpan.FromHours(6);
    }

    /// <summary>
    /// Determines whether a dinner time change should trigger a push notification.
    /// Returns true if and only if: (a) newDinnerTime is not null, AND (b) newDinnerTime differs
    /// from previousDinnerTime, AND (c) the naive dinner DateTime is within the 6-hour notification window.
    /// </summary>
    internal static bool ShouldNotifyDinnerTimeChange(TimeOnly? previousDinnerTime, TimeOnly? newDinnerTime, DateTimeOffset currentUtcTime, int timezoneOffsetMinutes, DateOnly date)
    {
        if (newDinnerTime is null)
            return false;

        if (newDinnerTime == previousDinnerTime)
            return false;

        return IsDinnerTimeWithinWindow(date, newDinnerTime.Value, timezoneOffsetMinutes, currentUtcTime);
    }

    /// <summary>Selects the consolidated notification translation key and parameters based on what changed.</summary>
    private static (string TranslationKey, string Parameters) GetNotificationKeyAndParameters(
        string description, TimeOnly? dinnerTime, bool shouldNotifyDish, bool shouldNotifyDinnerTime)
    {
        if (shouldNotifyDish && shouldNotifyDinnerTime)
        {
            var parameters = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["description"] = description,
                ["time"] = dinnerTime!.Value.ToString("HH:mm")
            });
            return (TranslationKeys.NotificationDishAndDinnerTimeChanged, parameters);
        }

        if (shouldNotifyDinnerTime)
        {
            var parameters = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["time"] = dinnerTime!.Value.ToString("HH:mm")
            });
            return (TranslationKeys.NotificationDinnerTimeChanged, parameters);
        }

        // Only dish changed.
        var dishParameters = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["description"] = description
        });
        return (TranslationKeys.HistoryDishSet, dishParameters);
    }

    /// <inheritdoc/>
    public async Task<bool> UpsertCommentAsync(Guid householdId, DateOnly date, Guid housemateId, string text, Guid actingHousemateId, CancellationToken ct = default)
    {
        var housemate = await _housemateRepository.GetAsync(householdId, housemateId, ct);
        if (housemate is null)
            return false;

        var comment = new Comment(householdId, housemateId, date, text, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var parameters = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["name"] = housemateId.ToString(),
            ["text"] = text
        });
        var historyEntry = new DayHistoryEntry(
            householdId,
            date,
            DateTimeOffset.UtcNow,
            actingHousemateId,
            ChangeType.Comment,
            TranslationKeys.HistoryCommentSet,
            parameters);

        await Task.WhenAll(
            _commentRepository.UpsertAsync(comment, ct),
            _dayHistoryRepository.AddAsync(historyEntry, ct));

        // Send auto-notifications for today and tomorrow only; failures must not interrupt the save.
        if (IsTodayOrTomorrow(date))
            await _pushHandler.SendAutoNotificationsAsync(householdId, actingHousemateId, date, historyEntry.TranslationKey, historyEntry.Parameters, ct);

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteCommentAsync(Guid householdId, DateOnly date, Guid housemateId, Guid actingHousemateId, CancellationToken ct = default)
    {
        var housemate = await _housemateRepository.GetAsync(householdId, housemateId, ct);
        if (housemate is null)
            return false;

        var parameters = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["name"] = housemateId.ToString(),
        });
        var historyEntry = new DayHistoryEntry(
            householdId,
            date,
            DateTimeOffset.UtcNow,
            actingHousemateId,
            ChangeType.Comment,
            TranslationKeys.HistoryCommentDeleted,
            parameters);

        await Task.WhenAll(
            _commentRepository.DeleteAsync(householdId, date, housemateId, ct),
            _dayHistoryRepository.AddAsync(historyEntry, ct));

        // Send auto-notifications for today and tomorrow only; failures must not interrupt the save.
        if (IsTodayOrTomorrow(date))
            await _pushHandler.SendAutoNotificationsAsync(householdId, actingHousemateId, date, historyEntry.TranslationKey, historyEntry.Parameters, ct);

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> UpsertChefStatusAsync(Guid householdId, DateOnly date, Guid housemateId, bool isChef, Guid actingHousemateId, CancellationToken cancellationToken = default)
    {
        var housemate = await _housemateRepository.GetAsync(householdId, housemateId, cancellationToken);
        if (housemate is null || housemate.IsDeleted)
            return false;

        var parameters = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["name"] = housemateId.ToString(),
            ["enabled"] = isChef ? "true" : "false"
        });
        var historyEntry = new DayHistoryEntry(
            householdId,
            date,
            DateTimeOffset.UtcNow,
            actingHousemateId,
            ChangeType.ChefStatusChanged,
            TranslationKeys.HistoryChefStatusChanged,
            parameters);

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
