using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Happie.Web.Http;

/// <summary>
/// Delegating handler that injects the JWT bearer token and active housemate ID
/// into every outgoing HTTP request by reading them from localStorage via JS interop.
/// When the API returns 401 Unauthorized, clears the session and redirects to the login page.
/// </summary>
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly IJSRuntime _jsRuntime;
    private readonly NavigationManager _navigationManager;

    public AuthHeaderHandler(IJSRuntime jsRuntime, NavigationManager navigationManager)
    {
        _jsRuntime = jsRuntime;
        _navigationManager = navigationManager;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _jsRuntime.InvokeAsync<string?>(
            "localStorage.getItem",
            cancellationToken,
            "jwt");

        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var activeHousemateId = await _jsRuntime.InvokeAsync<string?>(
            "localStorage.getItem",
            cancellationToken,
            "activeHousemateId");

        if (!string.IsNullOrWhiteSpace(activeHousemateId))
            request.Headers.TryAddWithoutValidation("X-Housemate-Id", activeHousemateId);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Store the current page URL so the user is redirected back after re-login.
            var currentUri = _navigationManager.ToBaseRelativePath(_navigationManager.Uri);
            if (!string.IsNullOrWhiteSpace(currentUri))
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, "returnUrl", "/" + currentUri);

            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, "jwt");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, "activeHousemateId");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, "activeHousemateName");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, "activeHousemateColor");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, "householdId");
            _navigationManager.NavigateTo("/", forceLoad: true);
        }

        return response;
    }
}
