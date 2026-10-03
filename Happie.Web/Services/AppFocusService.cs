using Microsoft.JSInterop;

namespace Happie.Web.Services;

/// <summary>Wraps document visibilitychange and window focus events via JS interop, firing a callback only when the page transitions from hidden to visible.</summary>
public class AppFocusService : IAppFocusService
{
    private readonly IJSRuntime _jsRuntime;
    private DotNetObjectReference<AppFocusService>? _dotNetReference;

    public AppFocusService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <inheritdoc />
    public event Func<Task>? OnAppFocus;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _dotNetReference = DotNetObjectReference.Create(this);

        // Register visibility/focus event listeners that call back into .NET.
        await _jsRuntime.InvokeVoidAsync("happie.registerFocusListener", _dotNetReference);
    }

    /// <summary>Called from JavaScript when the app transitions from hidden to visible.</summary>
    [JSInvokable]
    public async Task OnFocusChangedCallback()
    {
        if (OnAppFocus is not null)
            await OnAppFocus.Invoke();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_dotNetReference is not null)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("happie.unregisterFocusListener");
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
