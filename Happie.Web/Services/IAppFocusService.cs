namespace Happie.Web.Services;

/// <summary>Provides reactive app focus detection via browser visibility/focus events.</summary>
public interface IAppFocusService : IAsyncDisposable
{
    /// <summary>Raised when the app transitions from hidden to visible (e.g. iOS PWA foregrounded after being backgrounded).</summary>
    event Func<Task>? OnAppFocus;

    /// <summary>Registers the JS interop listeners for visibilitychange and focus events.</summary>
    Task InitializeAsync();
}
