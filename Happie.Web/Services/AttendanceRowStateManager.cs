using Happie.Shared.Domain;

namespace Happie.Web.Services;

/// <summary>Encapsulates expand/collapse state management for attendance slide-in buttons.</summary>
public class AttendanceRowStateManager : IDisposable
{
    private Guid? _expandedHousemateId;
    private readonly HashSet<Guid> _animatingIds = new();
    private ITimerHandle? _autoCollapseTimer;
    private bool _isNarrowViewport;
    private bool _hasPointerDevice;
    private bool _expandedViaHover;
    private readonly int _autoCollapseIntervalMs;
    private readonly int _animationDurationMs;
    private readonly IDelayService _delayService;

    /// <summary>Creates a new instance with the default 5-second auto-collapse interval and 250ms animation duration.</summary>
    public AttendanceRowStateManager() : this(new RealDelayService(), 5000, 250)
    {
    }

    /// <summary>Creates a new instance with custom timing and delay service (for testing).</summary>
    public AttendanceRowStateManager(IDelayService delayService, int autoCollapseIntervalMs = 5000, int animationDurationMs = 250)
    {
        _delayService = delayService;
        _autoCollapseIntervalMs = autoCollapseIntervalMs;
        _animationDurationMs = animationDurationMs;
    }

    /// <summary>Raised after any state change to trigger component re-render.</summary>
    public event Action? StateChanged;

    /// <summary>Whether the collapse/expand behavior is active (narrow viewport only).</summary>
    public bool IsCollapseEnabled => _isNarrowViewport;

    /// <summary>Whether the auto-collapse timer is currently running.</summary>
    public bool IsAutoCollapseTimerActive => _autoCollapseTimer is not null && _autoCollapseTimer.IsActive;

    /// <summary>Determines if a housemate row is in expanded state.</summary>
    public bool IsExpanded(Guid housemateId) => _expandedHousemateId == housemateId;

    /// <summary>Determines if a housemate row is animating (interactions locked).</summary>
    public bool IsAnimating(Guid housemateId) => _animatingIds.Contains(housemateId);

    /// <summary>Returns the active button status for a housemate row.</summary>
    public AttendanceStatus GetActiveStatus(Guid housemateId, AttendanceStatus currentStatus) => currentStatus;

    /// <summary>Sets initial state from JS interop results.</summary>
    public void Configure(bool isNarrowViewport, bool hasPointerDevice)
    {
        _isNarrowViewport = isNarrowViewport;
        _hasPointerDevice = hasPointerDevice;
    }

    /// <summary>Expands a row, collapsing any other expanded row first. Starts auto-collapse timer.</summary>
    public async Task ExpandAsync(Guid housemateId)
    {
        if (!_isNarrowViewport)
            return;

        if (_animatingIds.Contains(housemateId))
            return;

        // Single-row policy: collapse the previously expanded row.
        if (_expandedHousemateId is not null && _expandedHousemateId.Value != housemateId)
        {
            var previousId = _expandedHousemateId.Value;
            _animatingIds.Add(previousId);
            _expandedHousemateId = null;
            CancelAutoCollapseTimer();

            // Clear animation lock for the previous row after 250ms.
            _ = ClearAnimationAfterDelayAsync(previousId);
        }

        // Add animation lock for the expanding row.
        _animatingIds.Add(housemateId);
        _expandedHousemateId = housemateId;

        // Start auto-collapse timer unless expanded via hover.
        if (!_expandedViaHover)
            StartAutoCollapseTimer();

        StateChanged?.Invoke();

        // Clear animation lock after 250ms.
        await ClearAnimationAfterDelayAsync(housemateId);
    }

    /// <summary>Collapses the specified row with animation.</summary>
    public async Task CollapseAsync(Guid housemateId)
    {
        if (!_isNarrowViewport)
            return;

        if (_animatingIds.Contains(housemateId))
            return;

        if (_expandedHousemateId != housemateId)
            return;

        _animatingIds.Add(housemateId);
        _expandedHousemateId = null;
        CancelAutoCollapseTimer();

        StateChanged?.Invoke();

        // Clear animation lock after 250ms.
        await ClearAnimationAfterDelayAsync(housemateId);
    }

    /// <summary>Handles the active button click — expands if collapsed, collapses if already expanded (re-tap).</summary>
    public async Task HandleActiveButtonClickAsync(Guid housemateId)
    {
        if (!_isNarrowViewport)
            return;

        if (_animatingIds.Contains(housemateId))
            return;

        _expandedViaHover = false;

        if (_expandedHousemateId == housemateId)
        {
            // Re-tap: collapse.
            await CollapseAsync(housemateId);
            return;
        }

        // Collapsed: expand.
        await ExpandAsync(housemateId);
    }

    /// <summary>Handles an attendance button click in expanded state.</summary>
    public async Task<ExpandedButtonClickResult> HandleExpandedButtonClickAsync(
        Guid housemateId, AttendanceStatus currentStatus, AttendanceStatus newStatus)
    {
        _expandedViaHover = false;
        CancelAutoCollapseTimer();

        if (_expandedHousemateId == housemateId && !_animatingIds.Contains(housemateId))
            await CollapseAsync(housemateId);

        return new ExpandedButtonClickResult(newStatus != currentStatus);
    }

    /// <summary>Handles pointer entering the active button area — expands via hover.</summary>
    public async Task HandleMouseEnterAsync(Guid housemateId)
    {
        if (!_hasPointerDevice || !_isNarrowViewport)
            return;

        if (_animatingIds.Contains(housemateId))
            return;

        if (_expandedHousemateId is not null)
            return;

        _expandedViaHover = true;
        await ExpandAsync(housemateId);
    }

    /// <summary>Handles pointer leaving the row boundary — collapses if expanded via hover.</summary>
    public async Task HandleMouseLeaveAsync(Guid housemateId)
    {
        if (!_expandedViaHover)
            return;

        if (_expandedHousemateId != housemateId)
            return;

        if (_animatingIds.Contains(housemateId))
            return;

        _expandedViaHover = false;
        await CollapseAsync(housemateId);
    }

    /// <summary>Collapses current expanded row on outside click.</summary>
    public async Task HandleOutsideClickAsync()
    {
        if (_expandedHousemateId is null)
            return;

        if (_animatingIds.Contains(_expandedHousemateId.Value))
            return;

        await CollapseAsync(_expandedHousemateId.Value);
    }

    /// <summary>Called when the viewport media query changes (crosses 480px threshold).</summary>
    public async Task HandleViewportChangeAsync(bool isNarrow)
    {
        var wasNarrow = _isNarrowViewport;
        _isNarrowViewport = isNarrow;

        if (isNarrow && !wasNarrow)
        {
            // Transitioning to narrow: collapse all rows, cancel timer.
            _expandedHousemateId = null;
            CancelAutoCollapseTimer();
            StateChanged?.Invoke();
        }
        else if (!isNarrow && wasNarrow)
        {
            // Transitioning to wide: clear expanded state, cancel timer.
            _expandedHousemateId = null;
            CancelAutoCollapseTimer();
            StateChanged?.Invoke();
        }

        await Task.CompletedTask;
    }

    /// <summary>Disposes the auto-collapse timer.</summary>
    public void Dispose()
    {
        _autoCollapseTimer?.Dispose();
        _autoCollapseTimer = null;
    }

    private void StartAutoCollapseTimer()
    {
        CancelAutoCollapseTimer();

        _autoCollapseTimer = _delayService.StartTimer(_autoCollapseIntervalMs, async () =>
        {
            if (_expandedHousemateId is not null && !_animatingIds.Contains(_expandedHousemateId.Value))
            {
                var housemateId = _expandedHousemateId.Value;
                _animatingIds.Add(housemateId);
                _expandedHousemateId = null;
                _autoCollapseTimer = null;
                StateChanged?.Invoke();
                await ClearAnimationAfterDelayAsync(housemateId);
            }
        });
    }

    private void CancelAutoCollapseTimer()
    {
        _autoCollapseTimer?.Cancel();
        _autoCollapseTimer = null;
    }

    private async Task ClearAnimationAfterDelayAsync(Guid housemateId)
    {
        await _delayService.DelayAsync(_animationDurationMs);
        _animatingIds.Remove(housemateId);
        StateChanged?.Invoke();
    }
}
