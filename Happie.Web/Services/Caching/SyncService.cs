using System.Text;
using System.Text.Json;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;
using Happie.Web.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace Happie.Web.Services.Caching;

/// <summary>Replays queued mutations when connectivity is restored, with exponential backoff retry and rollback.</summary>
public class SyncService : ISyncService
{
    private readonly IMutationQueue _mutationQueue;
    private readonly ICacheStore _cacheStore;
    private readonly IConnectivityService _connectivityService;
    private readonly LoadingIndicatorState _loadingIndicatorState;
    private readonly SyncToastState _syncToastState;
    private readonly HttpClient _httpClient;
    private readonly IDelayService _delayService;
    private readonly IStringLocalizer<AppStrings> _localizer;
    private readonly IJSRuntime _jsRuntime;

    private const int MaxRetryAttempts = 5;
    private const int ReplayDelayMs = 5000;

    private ITimerHandle? _replayTimer;
    private bool _isReplaying;
    private bool _disposed;

    public event Action<string, DayPlanResponse>? OnDayPlanRolledBack;

    public SyncService(
        IMutationQueue mutationQueue,
        ICacheStore cacheStore,
        IConnectivityService connectivityService,
        LoadingIndicatorState loadingIndicatorState,
        SyncToastState syncToastState,
        HttpClient httpClient,
        IDelayService delayService,
        IStringLocalizer<AppStrings> localizer,
        IJSRuntime jsRuntime)
    {
        _mutationQueue = mutationQueue;
        _cacheStore = cacheStore;
        _connectivityService = connectivityService;
        _loadingIndicatorState = loadingIndicatorState;
        _syncToastState = syncToastState;
        _httpClient = httpClient;
        _delayService = delayService;
        _localizer = localizer;
        _jsRuntime = jsRuntime;
    }

    /// <inheritdoc />
    public Task InitializeAsync()
    {
        _connectivityService.OnConnectivityChanged += OnConnectivityChanged;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _connectivityService.OnConnectivityChanged -= OnConnectivityChanged;
        _replayTimer?.Cancel();
        _replayTimer = null;
    }

    private void OnConnectivityChanged(bool isOnline)
    {
        if (!isOnline)
            return;

        // Cancel any existing timer before starting a new one.
        _replayTimer?.Cancel();

        // Start replay after a 5-second delay.
        _replayTimer = _delayService.StartTimer(ReplayDelayMs, async () =>
        {
            _replayTimer = null;
            await ReplayMutationsAsync();
        });
    }

    private async Task ReplayMutationsAsync()
    {
        if (_isReplaying)
            return;

        _isReplaying = true;

        var householdId = await GetHouseholdIdAsync();
        if (householdId is null)
        {
            _isReplaying = false;
            return;
        }

        _loadingIndicatorState.IncrementAsync();

        try
        {
            while (true)
            {
                var mutation = await _mutationQueue.DequeueAsync(householdId);
                if (mutation is null)
                    break;

                await ReplayMutationAsync(householdId, mutation);
            }
        }
        finally
        {
            _loadingIndicatorState.DecrementAsync();
            _isReplaying = false;
        }
    }

    private async Task ReplayMutationAsync(string householdId, QueuedMutation mutation)
    {
        var retryAttempt = 0;

        while (true)
        {
            HttpResponseMessage? response = null;

            try
            {
                response = await SendMutationRequestAsync(mutation);
            }
            catch
            {
                // Network error — retry with backoff.
                retryAttempt++;

                if (retryAttempt > MaxRetryAttempts)
                {
                    await HandleExhaustedRetriesAsync(householdId, mutation);
                    return;
                }

                var delayMs = CalculateBackoffDelay(retryAttempt);
                await _delayService.DelayAsync(delayMs);
                continue;
            }

            var statusCode = (int)response.StatusCode;

            if (statusCode >= 200 && statusCode < 300)
            {
                // Success — mutation already dequeued.
                return;
            }

            if (statusCode == 409)
            {
                // Conflict — another housemate made a more recent change.
                await RollbackMutationAsync(householdId, mutation);
                ShowConflictToast(mutation);
                return;
            }

            if (statusCode >= 400 && statusCode < 500)
            {
                // Client error — discard and roll back.
                await RollbackMutationAsync(householdId, mutation);
                ShowFailureToast(mutation);
                return;
            }

            if (statusCode >= 500)
            {
                // Server error — retry with backoff.
                retryAttempt++;

                if (retryAttempt > MaxRetryAttempts)
                {
                    await HandleExhaustedRetriesAsync(householdId, mutation);
                    return;
                }

                var delayMs = CalculateBackoffDelay(retryAttempt);
                await _delayService.DelayAsync(delayMs);
            }
        }
    }

    private async Task<HttpResponseMessage> SendMutationRequestAsync(QueuedMutation mutation)
    {
        var request = new HttpRequestMessage(new HttpMethod(mutation.Method), mutation.Url);

        // Add stored headers (Authorization, X-Housemate-Id).
        foreach (var header in mutation.Headers)
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);

        // Add If-Unmodified-Since header from mutation's createdAt timestamp.
        request.Headers.IfUnmodifiedSince = mutation.CreatedAt;

        // Add body if present.
        if (mutation.Body is not null)
            request.Content = new StringContent(mutation.Body, Encoding.UTF8, "application/json");

        return await _httpClient.SendAsync(request);
    }

    private async Task HandleExhaustedRetriesAsync(string householdId, QueuedMutation mutation)
    {
        await RollbackMutationAsync(householdId, mutation);
        ShowFailureToast(mutation);
    }

    private async Task RollbackMutationAsync(string householdId, QueuedMutation mutation)
    {
        var dateString = mutation.Date.ToString("yyyy-MM-dd");
        var cached = await _cacheStore.GetDayPlanAsync(householdId, dateString);

        // If the cache entry has been evicted or overwritten, no rollback is needed.
        if (cached is null)
            return;

        var dayPlan = JsonSerializer.Deserialize<DayPlanResponse>(cached.ResponseJson);
        if (dayPlan is null)
            return;

        var rolledBackDayPlan = RollbackMutation(dayPlan, mutation);
        if (rolledBackDayPlan is null)
            return;

        var updatedJson = JsonSerializer.Serialize(rolledBackDayPlan);
        await _cacheStore.PutDayPlanAsync(householdId, dateString, updatedJson);

        // Notify the UI so it re-renders with the rolled-back state.
        OnDayPlanRolledBack?.Invoke(dateString, rolledBackDayPlan);
    }

    private static DayPlanResponse? RollbackMutation(DayPlanResponse dayPlan, QueuedMutation mutation)
    {
        return mutation.MutationType switch
        {
            "attendance" => RollbackAttendance(dayPlan, mutation),
            "dish" => RollbackDish(dayPlan, mutation),
            "comment" => RollbackComment(dayPlan, mutation),
            _ => null
        };
    }

    private static DayPlanResponse? RollbackAttendance(DayPlanResponse dayPlan, QueuedMutation mutation)
    {
        // Parse the housemateId from the URL: days/{date}/attendance/{housemateId}.
        var segments = mutation.Url.Split('/');
        if (segments.Length < 4)
            return null;

        if (!Guid.TryParse(segments[^1], out var housemateId))
            return null;

        // Revert attendance to Unknown (the safe default when we can't determine the previous state).
        var updatedAttendance = dayPlan.Attendance
            .Select(x => x.HousemateId == housemateId ? x with { Status = AttendanceStatus.Unknown } : x)
            .ToList();

        return dayPlan with { Attendance = updatedAttendance };
    }

    private static DayPlanResponse? RollbackDish(DayPlanResponse dayPlan, QueuedMutation mutation)
    {
        // Revert dish to null (we cannot determine the previous dish value).
        return dayPlan with { Dish = null };
    }

    private static DayPlanResponse? RollbackComment(DayPlanResponse dayPlan, QueuedMutation mutation)
    {
        // Parse the housemateId from the URL: days/{date}/comments/{housemateId}.
        var segments = mutation.Url.Split('/');
        if (segments.Length < 4)
            return null;

        if (!Guid.TryParse(segments[^1], out var housemateId))
            return null;

        if (mutation.Method == "DELETE")
        {
            // DELETE was optimistically applied (removed comment). We cannot restore the original text,
            // so we leave the comment removed. The next background refresh will restore the correct state.
            return dayPlan;
        }

        // PUT comment was optimistically applied. Remove the comment that was added/updated.
        var updatedComments = dayPlan.Comments
            .Where(x => x.HousemateId != housemateId)
            .ToList();

        return dayPlan with { Comments = updatedComments };
    }

    private void ShowConflictToast(QueuedMutation mutation)
    {
        var mutationTypeLocalized = GetLocalizedMutationType(mutation.MutationType);
        var dateLocalized = mutation.Date.ToString("d MMMM");
        var message = _localizer["Sync_ConflictMessage", mutationTypeLocalized, dateLocalized];
        _syncToastState.ShowToast(message);
    }

    private void ShowFailureToast(QueuedMutation mutation)
    {
        var mutationTypeLocalized = GetLocalizedMutationType(mutation.MutationType);
        var dateLocalized = mutation.Date.ToString("d MMMM");
        var message = _localizer["Sync_FailureMessage", mutationTypeLocalized, dateLocalized];
        _syncToastState.ShowToast(message);
    }

    private string GetLocalizedMutationType(string mutationType)
    {
        return mutationType switch
        {
            "attendance" => _localizer["Sync_MutationType_Attendance"],
            "dish" => _localizer["Sync_MutationType_Dish"],
            "comment" => _localizer["Sync_MutationType_Comment"],
            _ => mutationType
        };
    }

    /// <summary>Calculates exponential backoff delay: min(2^N * 1000, 60000) ms.</summary>
    internal static int CalculateBackoffDelay(int retryAttempt)
    {
        var delay = (int)Math.Pow(2, retryAttempt) * 1000;
        return Math.Min(delay, 60000);
    }

    private async Task<string?> GetHouseholdIdAsync()
    {
        return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "householdId");
    }
}
