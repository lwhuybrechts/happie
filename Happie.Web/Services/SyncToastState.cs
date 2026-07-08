namespace Happie.Web.Services;

/// <summary>Manages sync failure toast notifications with a maximum of 3 visible simultaneously.</summary>
public class SyncToastState
{
    private readonly IDelayService _delayService;
    private readonly List<SyncToastItem> _visibleToasts = new();
    private readonly Queue<SyncToastItem> _pendingToasts = new();
    private const int MaxVisibleToasts = 3;
    private const int AutoDismissMs = 8000;

    public SyncToastState(IDelayService delayService)
    {
        _delayService = delayService;
    }

    /// <summary>The currently visible toast notifications (max 3).</summary>
    public IReadOnlyList<SyncToastItem> VisibleToasts => _visibleToasts;

    /// <summary>Fires when the visible toasts collection changes.</summary>
    public event Action? OnStateChanged;

    /// <summary>Adds a toast to the queue. If fewer than 3 are visible, it is shown immediately.</summary>
    public void ShowToast(string message, ToastType type = ToastType.Error)
    {
        var toast = new SyncToastItem(Guid.NewGuid(), message, type);

        if (_visibleToasts.Count < MaxVisibleToasts)
            ShowToastImmediately(toast);
        else
            _pendingToasts.Enqueue(toast);
    }

    /// <summary>Dismisses a toast by its identifier and promotes the next queued toast if available.</summary>
    public void DismissToast(Guid toastId)
    {
        var toast = _visibleToasts.FirstOrDefault(x => x.Id == toastId);
        if (toast is null)
            return;

        toast.DismissTimer?.Cancel();
        _visibleToasts.Remove(toast);

        PromoteNextToast();
        OnStateChanged?.Invoke();
    }

    private void ShowToastImmediately(SyncToastItem toast)
    {
        var timer = _delayService.StartTimer(AutoDismissMs, () =>
        {
            DismissToast(toast.Id);
            return Task.CompletedTask;
        });

        toast.DismissTimer = timer;
        _visibleToasts.Add(toast);
        OnStateChanged?.Invoke();
    }

    private void PromoteNextToast()
    {
        while (_visibleToasts.Count < MaxVisibleToasts && _pendingToasts.Count > 0)
        {
            var next = _pendingToasts.Dequeue();
            ShowToastImmediately(next);
        }
    }
}
