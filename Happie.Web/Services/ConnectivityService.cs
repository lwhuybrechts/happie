using Microsoft.JSInterop;

namespace Happie.Web.Services;

/// <summary>Wraps navigator.onLine and online/offline window events via JS interop.</summary>
public class ConnectivityService : IConnectivityService
{
    private readonly IJSRuntime _jsRuntime;
    private DotNetObjectReference<ConnectivityService>? _dotNetReference;

    public ConnectivityService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <inheritdoc />
    public bool IsOnline { get; private set; } = true;

    /// <inheritdoc />
    public event Action<bool>? OnConnectivityChanged;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _dotNetReference = DotNetObjectReference.Create(this);

        // Read the initial connectivity state from navigator.onLine.
        IsOnline = await _jsRuntime.InvokeAsync<bool>("happie.getOnlineStatus");

        // Register online/offline event listeners that call back into .NET.
        await _jsRuntime.InvokeVoidAsync("happie.registerConnectivityListener", _dotNetReference);
    }

    /// <summary>Called from JavaScript when the connectivity state changes.</summary>
    [JSInvokable]
    public void OnConnectivityChangedCallback(bool isOnline)
    {
        if (IsOnline == isOnline)
            return;

        IsOnline = isOnline;
        OnConnectivityChanged?.Invoke(isOnline);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_dotNetReference is not null)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("happie.unregisterConnectivityListener");
            }
            catch (JSDisconnectedException)
            {
                // Circuit is already disconnected; nothing to clean up.
            }

            _dotNetReference.Dispose();
            _dotNetReference = null;
        }
    }
}
