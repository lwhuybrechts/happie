namespace Happie.Web.Services;

/// <summary>Visual style of a toast notification.</summary>
public enum ToastType
{
    Error,
    Info
}

/// <summary>Represents a single toast notification.</summary>
public class SyncToastItem
{
    public SyncToastItem(Guid id, string message, ToastType type = ToastType.Error)
    {
        Id = id;
        Message = message;
        Type = type;
    }

    /// <summary>Unique identifier for this toast.</summary>
    public Guid Id { get; }

    /// <summary>The localized message to display.</summary>
    public string Message { get; }

    /// <summary>Visual style of this toast.</summary>
    public ToastType Type { get; }

    /// <summary>Timer handle for auto-dismiss. Set internally by SyncToastState.</summary>
    internal ITimerHandle? DismissTimer { get; set; }
}
