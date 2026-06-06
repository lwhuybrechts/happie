using Happie.Web.Services.Caching;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Happie.Web.Services;

/// <summary>Manages session state: provides logout functionality by clearing stored credentials and redirecting to the login page.</summary>
public class SessionService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly NavigationManager _navigationManager;
    private readonly ICacheStore _cacheStore;

    public SessionService(IJSRuntime jsRuntime, NavigationManager navigationManager, ICacheStore cacheStore)
    {
        _jsRuntime = jsRuntime;
        _navigationManager = navigationManager;
        _cacheStore = cacheStore;
    }

    /// <summary>Clears the JWT and active housemate data from localStorage and navigates to the login page.</summary>
    public async Task LogoutAsync()
    {
        // Read householdId before clearing localStorage so we can clear the cache.
        var householdId = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "householdId");

        if (!string.IsNullOrEmpty(householdId))
            await _cacheStore.ClearAllAsync(householdId);

        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "jwt");
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "activeHousemateId");
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "activeHousemateName");
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "activeHousemateColor");
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "householdId");

        _navigationManager.NavigateTo("/");
    }
}
