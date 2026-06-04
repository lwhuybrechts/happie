namespace Happie.Web.Services;

/// <summary>Production implementation that uses real timers and delays.</summary>
public class RealDelayService : IDelayService
{
    public Task DelayAsync(int milliseconds) => Task.Delay(milliseconds);

    public ITimerHandle StartTimer(int intervalMs, Func<Task> callback)
    {
        return new RealTimerHandle(intervalMs, callback);
    }

    private sealed class RealTimerHandle : ITimerHandle
    {
        private System.Timers.Timer? _timer;

        public RealTimerHandle(int intervalMs, Func<Task> callback)
        {
            _timer = new System.Timers.Timer(intervalMs);
            _timer.AutoReset = false;
            _timer.Elapsed += async (_, _) => await callback();
            _timer.Start();
        }

        public bool IsActive => _timer is not null && _timer.Enabled;

        public void Cancel()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
        }

        public void Dispose() => Cancel();
    }
}
