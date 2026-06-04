using Happie.Web.Services;

namespace Happie.Web.Tests.Helpers;

/// <summary>Test double for IDelayService that completes delays instantly and allows manual timer triggering.</summary>
public class FakeDelayService : IDelayService
{
    private FakeTimerHandle? _activeTimer;
    private readonly bool _blockDelays;
    private readonly List<TaskCompletionSource> _pendingDelays = new();

    /// <summary>Creates a FakeDelayService. When blockDelays is true, delays block until manually released.</summary>
    public FakeDelayService(bool blockDelays = false)
    {
        _blockDelays = blockDelays;
    }

    /// <summary>Completes immediately unless blockDelays is true, in which case it blocks until released.</summary>
    public Task DelayAsync(int milliseconds)
    {
        if (!_blockDelays)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource();
        _pendingDelays.Add(tcs);
        return tcs.Task;
    }

    /// <summary>Creates a timer that can be triggered manually via <see cref="TriggerTimerAsync"/>.</summary>
    public ITimerHandle StartTimer(int intervalMs, Func<Task> callback)
    {
        _activeTimer = new FakeTimerHandle(callback);
        return _activeTimer;
    }

    /// <summary>Manually fires the pending timer callback. Returns immediately if no timer is active.</summary>
    public async Task TriggerTimerAsync()
    {
        if (_activeTimer is null || !_activeTimer.IsActive)
            return;

        await _activeTimer.FireAsync();
    }

    /// <summary>Releases all pending delays so blocked tasks can continue.</summary>
    public void ReleaseAllDelays()
    {
        foreach (var tcs in _pendingDelays)
            tcs.TrySetResult();
        _pendingDelays.Clear();
    }

    private sealed class FakeTimerHandle : ITimerHandle
    {
        private readonly Func<Task> _callback;
        private bool _isActive = true;

        public FakeTimerHandle(Func<Task> callback)
        {
            _callback = callback;
        }

        public bool IsActive => _isActive;

        public async Task FireAsync()
        {
            if (!_isActive)
                return;

            _isActive = false;
            await _callback();
        }

        public void Cancel()
        {
            _isActive = false;
        }

        public void Dispose() => Cancel();
    }
}
