using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Happie.Web.Services;

/// <summary>Manages session state: provides logout functionality by clearing stored credentials and redirecting to the login page.</summary>
public class SessionService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly NavigationManager _navigationManager;

    public SessionService(IJSRuntime jsRuntime, NavigationManager navigationManager)
    {
        _jsRuntime = jsRuntime;
        _navigationManager = navigationManager;
    }

    /// <summary>Clears the JWT and active housemate data from localStorage and navigates to the login page.</summary>
    public async Task LogoutAsync()
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "jwt");
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "activeHousemateId");
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "activeHousemateName");
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "activeHousemateColor");
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "householdId");

        _navigationManager.NavigateTo("/");
    }
}
