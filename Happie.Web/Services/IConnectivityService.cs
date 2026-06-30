namespace Happie.Web.Services;

/// <summary>Provides reactive online/offline connectivity state via browser events.</summary>
public interface IConnectivityService : IAsyncDisposable
{
    /// <summary>Whether the device currently has network connectivity.</summary>
    bool IsOnline { get; }

    /// <summary>Raised when connectivity changes. The parameter is the new IsOnline state.</summary>
    event Action<bool>? OnConnectivityChanged;

    /// <summary>Initializes the service by reading the current connectivity state and registering event listeners.</summary>
    Task InitializeAsync();
}
