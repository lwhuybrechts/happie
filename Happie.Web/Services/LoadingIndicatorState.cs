namespace Happie.Web.Services;

/// <summary>Tracks active background operations and exposes visibility state with a 500ms minimum duration.</summary>
public class LoadingIndicatorState
{
    private readonly IDelayService _delayService;
    private int _activeOperationCount;
    private ITimerHandle? _hideTimer;
    private DateTimeOffset _visibleSince;

    /// <summary>Minimum duration in milliseconds the indicator must remain visible.</summary>
    private const int MinimumVisibilityMs = 500;

    public LoadingIndicatorState(IDelayService delayService)
    {
        _delayService = delayService;
    }

    /// <summary>Whether the loading indicator should be displayed.</summary>
    public bool IsVisible { get; private set; }

    /// <summary>Fires when <see cref="IsVisible"/> changes, so UI components can re-render.</summary>
    public event Action? OnStateChanged;

    /// <summary>Called when a background operation starts.</summary>
    public void IncrementAsync()
    {
        _activeOperationCount++;

        // Cancel any pending hide timer since we have active operations again.
        if (_hideTimer is not null)
        {
            _hideTimer.Cancel();
            _hideTimer = null;
        }

        if (!IsVisible)
        {
            IsVisible = true;
            _visibleSince = DateTimeOffset.UtcNow;
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>Called when a background operation completes.</summary>
    public void DecrementAsync()
    {
        if (_activeOperationCount > 0)
            _activeOperationCount--;

        if (_activeOperationCount > 0)
            return;

        // All operations complete. Check if minimum visibility has elapsed.
        var elapsed = (DateTimeOffset.UtcNow - _visibleSince).TotalMilliseconds;

        if (elapsed >= MinimumVisibilityMs)
        {
            // Minimum visibility satisfied. Hide immediately.
            IsVisible = false;
            OnStateChanged?.Invoke();
        }
        else
        {
            // Start a timer for the remaining duration.
            var remaining = (int)(MinimumVisibilityMs - elapsed);
            _hideTimer = _delayService.StartTimer(remaining, () =>
            {
                _hideTimer = null;

                // Only hide if no new operations started during the wait.
                if (_activeOperationCount == 0)
                {
                    IsVisible = false;
                    OnStateChanged?.Invoke();
                }

                return Task.CompletedTask;
            });
        }
    }
}
