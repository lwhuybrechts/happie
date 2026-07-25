using System.Net.Http.Json;
using Happie.Shared.Contracts;
using Microsoft.Extensions.Configuration;

namespace Happie.Web.Services;

/// <summary>Reports the app version to the backend at most once per app lifecycle.</summary>
public class VersionTracker
{
    private readonly HttpClient _httpClient;
    private readonly IConnectivityService _connectivityService;
    private readonly IConfiguration _configuration;
    private bool _hasReported;

    public VersionTracker(
        HttpClient httpClient,
        IConnectivityService connectivityService,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _connectivityService = connectivityService;
        _configuration = configuration;
    }

    /// <summary>Checks preconditions and fires a background version report if applicable.</summary>
    public void ReportVersionAsync()
    {
        if (_hasReported)
            return;

        if (!_connectivityService.IsOnline)
            return;

        var version = _configuration["AppVersion"];

        if (string.IsNullOrEmpty(version) || version == "1.0.0")
            return;

        // Set the flag immediately to guarantee at-most-once semantics.
        _hasReported = true;

        // Fire-and-forget the HTTP call.
        _ = SendVersionAsync(version);
    }

    private async Task SendVersionAsync(string version)
    {
        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var request = new ReportVersionRequest(version);
            await _httpClient.PutAsJsonAsync("api/housemates/version", request, cancellationTokenSource.Token);
        }
        catch
        {
            // Silently discard all exceptions — version reporting is best-effort.
        }
    }
}
