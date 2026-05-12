using Microsoft.JSInterop;

namespace Happie.Web.Http;

/// <summary>
/// Delegating handler that injects the JWT bearer token and active housemate ID
/// into every outgoing HTTP request by reading them from localStorage via JS interop.
/// </summary>
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly IJSRuntime _jsRuntime;

    public AuthHeaderHandler(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
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

        return await base.SendAsync(request, cancellationToken);
    }
}
