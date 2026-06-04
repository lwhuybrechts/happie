namespace Happie.Web.Services;

/// <summary>Abstracts time-based operations for testability.</summary>
public interface IDelayService
{
    /// <summary>Returns a task that completes after the specified delay in milliseconds.</summary>
    Task DelayAsync(int milliseconds);

    /// <summary>Starts a one-shot timer that invokes the callback after the specified interval.</summary>
    ITimerHandle StartTimer(int intervalMs, Func<Task> callback);
}

/// <summary>Handle to a running timer that can be cancelled.</summary>
public interface ITimerHandle : IDisposable
{
    /// <summary>Whether the timer is currently active.</summary>
    bool IsActive { get; }

    /// <summary>Stops and disposes the timer.</summary>
    void Cancel();
}
