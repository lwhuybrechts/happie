using System.Net.Http.Json;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Happie.Web.Services;

/// <summary>Handles push notification permission requests and subscription registration with the backend.</summary>
public class PushNotificationService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly HttpClient _httpClient;
    private readonly LocaleService _localeService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PushNotificationService> _logger;

    private bool _hasRequestedThisSession;

    public PushNotificationService(
        IJSRuntime jsRuntime,
        HttpClient httpClient,
        LocaleService localeService,
        IConfiguration configuration,
        ILogger<PushNotificationService> logger)
    {
        _jsRuntime = jsRuntime;
        _httpClient = httpClient;
        _localeService = localeService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>Whether the user denied push notification permission.</summary>
    public bool PermissionDenied { get; private set; }

    /// <summary>Raised when the permission state changes so UI components can react.</summary>
    public event Action? OnPermissionStateChanged;

    /// <summary>
    /// Requests push notification permission if not already granted or denied.
    /// On grant, subscribes and registers with the backend.
    /// This method is non-blocking and safe to call multiple times per session.
    /// </summary>
    public async Task RequestPermissionAndSubscribeAsync()
    {
        if (_hasRequestedThisSession)
            return;

        _hasRequestedThisSession = true;

        try
        {
            var currentState = await _jsRuntime.InvokeAsync<string>("happie.getPushPermissionState");

            if (currentState == "granted")
            {
                // Already granted — subscribe silently.
                await SubscribeAndRegisterAsync();
                return;
            }

            if (currentState == "denied" || currentState == "unsupported")
            {
                PermissionDenied = currentState == "denied";
                OnPermissionStateChanged?.Invoke();
                return;
            }

            // Permission is "default" — request it.
            var result = await _jsRuntime.InvokeAsync<string>("happie.requestPushPermission");

            if (result == "granted")
                await SubscribeAndRegisterAsync();
            else
                PermissionDenied = result == "denied";

            OnPermissionStateChanged?.Invoke();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to request push notification permission.");
        }
    }

    private async Task SubscribeAndRegisterAsync()
    {
        var vapidPublicKey = _configuration["VapidPublicKey"];

        if (string.IsNullOrWhiteSpace(vapidPublicKey))
        {
            _logger.LogWarning("VapidPublicKey is not configured. Push subscription skipped.");
            return;
        }

        var subscription = await _jsRuntime.InvokeAsync<PushSubscriptionResult?>("happie.subscribePush", vapidPublicKey);

        if (subscription is null)
        {
            _logger.LogWarning("Push subscription returned null from the browser.");
            return;
        }

        var request = new PushSubscribeRequest(
            subscription.Endpoint,
            subscription.P256dh,
            subscription.Auth,
            _localeService.CurrentLocale);

        try
        {
            var response = await _httpClient.PostAsJsonAsync("push/subscribe", request);
            response.EnsureSuccessStatusCode();

            // Store credentials in IndexedDB so the Service Worker can renew the subscription.
            await StorePushCredentialsForServiceWorkerAsync(vapidPublicKey);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to register push subscription with the backend.");
        }
    }

    private async Task StorePushCredentialsForServiceWorkerAsync(string vapidPublicKey)
    {
        try
        {
            var jwt = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "jwt");
            var housemateId = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "activeHousemateId");

            if (string.IsNullOrWhiteSpace(jwt) || string.IsNullOrWhiteSpace(housemateId))
                return;

            await _jsRuntime.InvokeVoidAsync("happie.storePushCredentials", vapidPublicKey, jwt, housemateId);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to store push credentials for Service Worker renewal.");
        }
    }

    /// <summary>Represents the push subscription data returned from the JS interop call.</summary>
    private record PushSubscriptionResult(string Endpoint, string P256dh, string Auth);
}
