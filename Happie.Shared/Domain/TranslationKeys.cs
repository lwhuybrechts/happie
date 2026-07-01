namespace Happie.Shared.Domain;

/// <summary>Constants for all shared translation keys used in history entries and nudge messages.</summary>
public static class TranslationKeys
{
    // History keys.
    public const string HistoryAttendanceSet = "history_attendance_set";
    public const string HistoryAttendanceSetSelf = "history_attendance_set_self";
    public const string HistoryDishSet = "history_dish_set";
    public const string HistoryCommentSet = "history_comment_set";
    public const string HistoryCommentSetSelf = "history_comment_set_self";
    public const string HistoryCommentDeleted = "history_comment_deleted";
    public const string HistoryCommentDeletedSelf = "history_comment_deleted_self";
    public const string HistoryChefStatusChanged = "history_chef_status_changed";
    public const string HistoryChefStatusChangedSelf = "history_chef_status_changed_self";
    public const string HistoryDinnerTimeSet = "history_dinner_time_set";
    public const string HistoryDinnerTimeCleared = "history_dinner_time_cleared";
    public const string HistoryDishAndDinnerTimeSet = "history_dish_and_dinner_time_set";
    public const string HistoryDishSetDinnerTimeCleared = "history_dish_set_dinner_time_cleared";
    public const string HistoryDishDeleted = "history_dish_deleted";

    // Push notification date prefix keys.
    public const string PushDateToday = "push_date_today";
    public const string PushDateTomorrow = "push_date_tomorrow";

    // Nudge keys.
    public const string NudgePleaseAddAttendance = "nudge_please_add_attendance";
    public const string NudgeWhatWouldYouLikeToEat = "nudge_what_would_you_like_to_eat";
    public const string NudgeDinnerSoonWhatsYourPlan = "nudge_dinner_soon_whats_your_plan";

    /// <summary>All known translation keys for validation purposes.</summary>
    public static readonly IReadOnlySet<string> AllKeys = new HashSet<string>
    {
        HistoryAttendanceSet,
        HistoryAttendanceSetSelf,
        HistoryDishSet,
        HistoryCommentSet,
        HistoryCommentSetSelf,
        HistoryCommentDeleted,
        HistoryCommentDeletedSelf,
        HistoryChefStatusChanged,
        HistoryChefStatusChangedSelf,
        HistoryDinnerTimeSet,
        HistoryDinnerTimeCleared,
        HistoryDishAndDinnerTimeSet,
        HistoryDishSetDinnerTimeCleared,
        HistoryDishDeleted,
        PushDateToday,
        PushDateTomorrow,
        NudgePleaseAddAttendance,
        NudgeWhatWouldYouLikeToEat,
        NudgeDinnerSoonWhatsYourPlan,
    };
}
