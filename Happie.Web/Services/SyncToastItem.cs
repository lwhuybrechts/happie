namespace Happie.Web.Services;

/// <summary>Represents a single sync failure toast notification.</summary>
public class SyncToastItem
{
    public SyncToastItem(Guid id, string message)
    {
        Id = id;
        Message = message;
    }

    /// <summary>Unique identifier for this toast.</summary>
    public Guid Id { get; }

    /// <summary>The localized message to display.</summary>
    public string Message { get; }

    /// <summary>Timer handle for auto-dismiss. Set internally by SyncToastState.</summary>
    internal ITimerHandle? DismissTimer { get; set; }
}
