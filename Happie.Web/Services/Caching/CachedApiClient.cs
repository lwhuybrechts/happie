using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Happie.Web.Services.Caching;

/// <summary>Central API facade providing stale-while-revalidate caching for reads and offline queueing for writes.</summary>
public class CachedApiClient : ICachedApiClient
{
    private readonly ICacheStore _cacheStore;
    private readonly IMutationQueue _mutationQueue;
    private readonly IConnectivityService _connectivityService;
    private readonly LoadingIndicatorState _loadingIndicatorState;
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private readonly NavigationManager _navigationManager;
    private readonly SessionService _sessionService;

    public event Action<DayPlanResponse>? OnDayPlanUpdated;
    public event Action<CalendarResponse>? OnCalendarUpdated;

    public bool IsColdCacheFetch { get; private set; }
    public bool HasLoadError { get; private set; }

    public CachedApiClient(
        ICacheStore cacheStore,
        IMutationQueue mutationQueue,
        IConnectivityService connectivityService,
        LoadingIndicatorState loadingIndicatorState,
        HttpClient httpClient,
        IJSRuntime jsRuntime,
        NavigationManager navigationManager,
        SessionService sessionService)
    {
        _cacheStore = cacheStore;
        _mutationQueue = mutationQueue;
        _connectivityService = connectivityService;
        _loadingIndicatorState = loadingIndicatorState;
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
        _navigationManager = navigationManager;
        _sessionService = sessionService;
    }

    public async Task<DayPlanResponse?> GetDayPlanAsync(string date)
    {
        HasLoadError = false;
        var householdId = await GetHouseholdIdAsync();
        if (householdId is null)
        {
            await RedirectToLoginAsync();
            return null;
        }

        var cached = await _cacheStore.GetDayPlanAsync(householdId, date);

        if (cached is not null)
        {
            // Stale-while-revalidate: return cached immediately, background refresh if online.
            IsColdCacheFetch = false;
            var cachedResponse = JsonSerializer.Deserialize<DayPlanResponse>(cached.ResponseJson);

            if (_connectivityService.IsOnline)
                _ = BackgroundRefreshDayPlanAsync(householdId, date, cached.ResponseJson);

            return cachedResponse;
        }

        // Cold cache path.
        IsColdCacheFetch = true;

        if (!_connectivityService.IsOnline)
            return null;

        return await FetchAndCacheDayPlanAsync(householdId, date);
    }

    public async Task<DayPlanResponse?> RetryDayPlanAsync(string date)
    {
        HasLoadError = false;
        var householdId = await GetHouseholdIdAsync();
        if (householdId is null)
        {
            await RedirectToLoginAsync();
            return null;
        }

        IsColdCacheFetch = true;
        return await FetchAndCacheDayPlanAsync(householdId, date);
    }

    public async Task<CalendarResponse?> GetCalendarAsync(DateOnly viewedMonth)
    {
        HasLoadError = false;
        var householdId = await GetHouseholdIdAsync();
        if (householdId is null)
        {
            await RedirectToLoginAsync();
            return null;
        }

        var month = viewedMonth.ToString("yyyy-MM");
        var cached = await _cacheStore.GetCalendarAsync(householdId, month);

        if (cached is not null)
        {
            // Stale-while-revalidate: return cached immediately, background refresh if online.
            IsColdCacheFetch = false;
            var cachedResponse = JsonSerializer.Deserialize<CalendarResponse>(cached.ResponseJson);

            if (_connectivityService.IsOnline)
                _ = BackgroundRefreshCalendarAsync(householdId, viewedMonth, month, cached.ResponseJson);

            return cachedResponse;
        }

        // Cold cache path.
        IsColdCacheFetch = true;

        if (!_connectivityService.IsOnline)
            return null;

        return await FetchAndCacheCalendarAsync(householdId, viewedMonth, month);
    }

    public async Task<CalendarResponse?> RetryCalendarAsync(DateOnly viewedMonth)
    {
        HasLoadError = false;
        var householdId = await GetHouseholdIdAsync();
        if (householdId is null)
        {
            await RedirectToLoginAsync();
            return null;
        }

        var month = viewedMonth.ToString("yyyy-MM");
        IsColdCacheFetch = true;
        return await FetchAndCacheCalendarAsync(householdId, viewedMonth, month);
    }

    public async Task<bool> SaveAttendanceAsync(string date, Guid housemateId, AttendanceStatus status)
    {
        var householdId = await GetHouseholdIdAsync();
        if (householdId is null)
            return false;

        var url = $"days/{date}/attendance/{housemateId}";
        var request = new UpdateAttendanceRequest(status);

        if (_connectivityService.IsOnline)
        {
            var response = await _httpClient.PutAsJsonAsync(url, request);
            if (!response.IsSuccessStatusCode)
                return false;

            // Update day plan cache if entry exists.
            await ApplyAttendanceOptimisticUpdate(householdId, date, housemateId, status);

            // Update calendar cache in-place if entry exists.
            await UpdateCalendarOnAttendanceChange(householdId, date, housemateId, status);

            return true;
        }

        // Offline: enqueue mutation and apply optimistic update.
        await EnqueueMutationAsync(householdId, "PUT", url, JsonSerializer.Serialize(request), date, "attendance");
        await ApplyAttendanceOptimisticUpdate(householdId, date, housemateId, status);
        await UpdateCalendarOnAttendanceChange(householdId, date, housemateId, status);

        return true;
    }

    public async Task<bool> SaveDishAsync(string date, string description, int? dinnerTimeHour, int? dinnerTimeMinute, int timezoneOffsetMinutes)
    {
        var householdId = await GetHouseholdIdAsync();
        if (householdId is null)
            return false;

        var url = $"days/{date}/dish";
        var request = new UpdateDishRequest(description, dinnerTimeHour, dinnerTimeMinute, timezoneOffsetMinutes);

        if (_connectivityService.IsOnline)
        {
            var response = await _httpClient.PutAsJsonAsync(url, request);
            if (!response.IsSuccessStatusCode)
                return false;

            // Update day plan cache if entry exists.
            await ApplyDishOptimisticUpdate(householdId, date, description, dinnerTimeHour, dinnerTimeMinute);
            return true;
        }

        // Offline: enqueue mutation and apply optimistic update.
        await EnqueueMutationAsync(householdId, "PUT", url, JsonSerializer.Serialize(request), date, "dish");
        await ApplyDishOptimisticUpdate(householdId, date, description, dinnerTimeHour, dinnerTimeMinute);

        return true;
    }

    public async Task<bool> DeleteDishAsync(string date)
    {
        var householdId = await GetHouseholdIdAsync();
        if (householdId is null)
            return false;

        var url = $"days/{date}/dish";

        if (_connectivityService.IsOnline)
        {
            var response = await _httpClient.DeleteAsync(url);
            if (!response.IsSuccessStatusCode)
                return false;

            // Update day plan cache if entry exists.
            await ApplyDishDeleteOptimisticUpdate(householdId, date);
            return true;
        }

        // Offline: enqueue mutation and apply optimistic update.
        await EnqueueMutationAsync(householdId, "DELETE", url, null, date, "dish");
        await ApplyDishDeleteOptimisticUpdate(householdId, date);

        return true;
    }

    public async Task<bool> SaveChefStatusAsync(string date, Guid housemateId, bool isChef)
    {
        var householdId = await GetHouseholdIdAsync();
        if (householdId is null)
            return false;

        var url = $"days/{date}/chef/{housemateId}";
        var request = new UpdateChefStatusRequest(isChef);

        if (_connectivityService.IsOnline)
        {
            var response = await _httpClient.PutAsJsonAsync(url, request);
            if (!response.IsSuccessStatusCode)
                return false;

            // Update day plan cache if entry exists.
            await ApplyChefOptimisticUpdate(householdId, date, housemateId, isChef);
            return true;
        }

        // Offline: enqueue mutation and apply optimistic update.
        await EnqueueMutationAsync(householdId, "PUT", url, JsonSerializer.Serialize(request), date, "attendance");
        await ApplyChefOptimisticUpdate(householdId, date, housemateId, isChef);

        return true;
    }

    public async Task<bool> SaveCommentAsync(string date, Guid housemateId, string text)
    {
        var householdId = await GetHouseholdIdAsync();
        if (householdId is null)
            return false;

        var url = $"days/{date}/comments/{housemateId}";
        var body = JsonSerializer.Serialize(new { text });

        if (_connectivityService.IsOnline)
        {
            var response = await _httpClient.PutAsJsonAsync(url, new { text });
            if (!response.IsSuccessStatusCode)
                return false;

            // Update day plan cache if entry exists.
            await ApplyCommentOptimisticUpdate(householdId, date, housemateId, text);
            return true;
        }

        // Offline: enqueue mutation and apply optimistic update.
        await EnqueueMutationAsync(householdId, "PUT", url, body, date, "comment");
        await ApplyCommentOptimisticUpdate(householdId, date, housemateId, text);

        return true;
    }

    public async Task<bool> DeleteCommentAsync(string date, Guid housemateId)
    {
        var householdId = await GetHouseholdIdAsync();
        if (householdId is null)
            return false;

        var url = $"days/{date}/comments/{housemateId}";

        if (_connectivityService.IsOnline)
        {
            var response = await _httpClient.DeleteAsync(url);
            if (!response.IsSuccessStatusCode)
                return false;

            // Update day plan cache if entry exists.
            await ApplyCommentDeleteOptimisticUpdate(householdId, date, housemateId);
            return true;
        }

        // Offline: enqueue mutation and apply optimistic update.
        await EnqueueMutationAsync(householdId, "DELETE", url, null, date, "comment");
        await ApplyCommentDeleteOptimisticUpdate(householdId, date, housemateId);

        return true;
    }

    private async Task<DayPlanResponse?> FetchAndCacheDayPlanAsync(string householdId, string date)
    {
        try
        {
            var response = await _httpClient.GetAsync($"days/{date}");

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await ClearSessionAndRedirectAsync(householdId);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                HasLoadError = true;
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            await _cacheStore.PutDayPlanAsync(householdId, date, json);
            return JsonSerializer.Deserialize<DayPlanResponse>(json);
        }
        catch
        {
            HasLoadError = true;
            return null;
        }
    }

    private async Task<CalendarResponse?> FetchAndCacheCalendarAsync(string householdId, DateOnly viewedMonth, string month)
    {
        try
        {
            var (startDate, endDate) = CalendarGridService.GetVisibleDateRange(viewedMonth);
            var response = await _httpClient.GetAsync($"days?from={startDate:yyyy-MM-dd}&to={endDate:yyyy-MM-dd}");

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await ClearSessionAndRedirectAsync(householdId);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                HasLoadError = true;
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            await _cacheStore.PutCalendarAsync(householdId, month, json);
            return JsonSerializer.Deserialize<CalendarResponse>(json);
        }
        catch
        {
            HasLoadError = true;
            return null;
        }
    }

    private async Task BackgroundRefreshDayPlanAsync(string householdId, string date, string previousJson)
    {
        _loadingIndicatorState.IncrementAsync();
        try
        {
            var response = await _httpClient.GetAsync($"days/{date}");

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await ClearSessionAndRedirectAsync(householdId);
                return;
            }

            if (!response.IsSuccessStatusCode)
                return;

            var freshJson = await response.Content.ReadAsStringAsync();

            // Compare against the current cache state, not the original snapshot.
            // An optimistic update may have written newer data while this request was in-flight.
            var currentCached = await _cacheStore.GetDayPlanAsync(householdId, date);
            var currentJson = currentCached?.ResponseJson;

            if (freshJson == currentJson)
                return;

            // Only update cache and notify if no optimistic update has occurred since we started.
            // If the cache still matches what we originally read, it's safe to overwrite.
            if (currentJson == previousJson)
            {
                await _cacheStore.PutDayPlanAsync(householdId, date, freshJson);

                var freshResponse = JsonSerializer.Deserialize<DayPlanResponse>(freshJson);
                if (freshResponse is not null)
                    OnDayPlanUpdated?.Invoke(freshResponse);
            }
        }
        catch
        {
            // Network error or timeout: retain cached data silently.
        }
        finally
        {
            _loadingIndicatorState.DecrementAsync();
        }
    }

    private async Task BackgroundRefreshCalendarAsync(string householdId, DateOnly viewedMonth, string month, string previousJson)
    {
        _loadingIndicatorState.IncrementAsync();
        try
        {
            var (startDate, endDate) = CalendarGridService.GetVisibleDateRange(viewedMonth);
            var response = await _httpClient.GetAsync($"days?from={startDate:yyyy-MM-dd}&to={endDate:yyyy-MM-dd}");

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await ClearSessionAndRedirectAsync(householdId);
                return;
            }

            if (!response.IsSuccessStatusCode)
                return;

            var freshJson = await response.Content.ReadAsStringAsync();

            // Compare against the current cache state, not the original snapshot.
            // An optimistic update may have written newer data while this request was in-flight.
            var currentCached = await _cacheStore.GetCalendarAsync(householdId, month);
            var currentJson = currentCached?.ResponseJson;

            if (freshJson == currentJson)
                return;

            // Only update cache and notify if no optimistic update has occurred since we started.
            if (currentJson == previousJson)
            {
                await _cacheStore.PutCalendarAsync(householdId, month, freshJson);

                var freshResponse = JsonSerializer.Deserialize<CalendarResponse>(freshJson);
                if (freshResponse is not null)
                    OnCalendarUpdated?.Invoke(freshResponse);
            }
        }
        catch
        {
            // Network error or timeout: retain cached data silently.
        }
        finally
        {
            _loadingIndicatorState.DecrementAsync();
        }
    }

    private async Task ApplyAttendanceOptimisticUpdate(string householdId, string date, Guid housemateId, AttendanceStatus status)
    {
        var dayPlan = await GetCachedDayPlanAsync(householdId, date);
        if (dayPlan is null)
            return;

        var updatedAttendance = dayPlan.Attendance
            .Select(x => x.HousemateId == housemateId ? x with { Status = status } : x)
            .ToList();

        await SaveDayPlanUpdateAsync(householdId, date, dayPlan with { Attendance = updatedAttendance });
    }

    private async Task UpdateCalendarOnAttendanceChange(string householdId, string date, Guid housemateId, AttendanceStatus status)
    {
        var parsedDate = DateOnly.ParseExact(date, "yyyy-MM-dd");
        var month = parsedDate.ToString("yyyy-MM");

        var cached = await _cacheStore.GetCalendarAsync(householdId, month);
        if (cached is null)
            return;

        var calendar = JsonSerializer.Deserialize<CalendarResponse>(cached.ResponseJson);
        if (calendar is null)
            return;

        // Get the housemate color from the day plan cache if available.
        var dayPlan = await GetCachedDayPlanAsync(householdId, date);
        if (dayPlan is null)
            return;

        var housemate = dayPlan.Attendance.FirstOrDefault(x => x.HousemateId == housemateId);
        if (housemate is null)
            return;

        var updatedDays = calendar.Days.Select(x =>
        {
            if (x.Date != parsedDate)
                return x;

            var colors = x.EatingInColors.ToList();

            if (status == AttendanceStatus.EatingIn)
            {
                if (!colors.Contains(housemate.Color))
                    colors.Add(housemate.Color);
            }
            else
            {
                colors.Remove(housemate.Color);
            }

            return x with { EatingInColors = colors };
        }).ToList();

        var updatedCalendar = calendar with { Days = updatedDays };
        var updatedJson = JsonSerializer.Serialize(updatedCalendar);
        await _cacheStore.PutCalendarAsync(householdId, month, updatedJson);
    }

    private async Task ApplyDishOptimisticUpdate(string householdId, string date, string description, int? dinnerTimeHour, int? dinnerTimeMinute)
    {
        var dayPlan = await GetCachedDayPlanAsync(householdId, date);
        if (dayPlan is null)
            return;

        var housemateId = await GetActiveHousemateIdAsync();

        var updatedDish = dayPlan.Dish is not null
            ? dayPlan.Dish with { Description = description, LastChangedByHousemateId = housemateId ?? dayPlan.Dish.LastChangedByHousemateId, LastChangedAt = DateTimeOffset.UtcNow, DinnerTimeHour = dinnerTimeHour, DinnerTimeMinute = dinnerTimeMinute }
            : new DishDto(description, housemateId, DateTimeOffset.UtcNow, dinnerTimeHour, dinnerTimeMinute);

        await SaveDayPlanUpdateAsync(householdId, date, dayPlan with { Dish = updatedDish });
    }

    private async Task ApplyDishDeleteOptimisticUpdate(string householdId, string date)
    {
        var dayPlan = await GetCachedDayPlanAsync(householdId, date);
        if (dayPlan is null)
            return;

        await SaveDayPlanUpdateAsync(householdId, date, dayPlan with { Dish = null });
    }

    private async Task ApplyChefOptimisticUpdate(string householdId, string date, Guid housemateId, bool isChef)
    {
        var dayPlan = await GetCachedDayPlanAsync(householdId, date);
        if (dayPlan is null)
            return;

        var updatedAttendance = dayPlan.Attendance
            .Select(x => x.HousemateId == housemateId ? x with { IsChef = isChef } : x)
            .ToList();

        await SaveDayPlanUpdateAsync(householdId, date, dayPlan with { Attendance = updatedAttendance });
    }

    private async Task ApplyCommentOptimisticUpdate(string householdId, string date, Guid housemateId, string text)
    {
        var dayPlan = await GetCachedDayPlanAsync(householdId, date);
        if (dayPlan is null)
            return;

        var existingComment = dayPlan.Comments.FirstOrDefault(x => x.HousemateId == housemateId);
        IReadOnlyList<CommentDto> updatedComments;

        if (existingComment is not null)
        {
            updatedComments = dayPlan.Comments
                .Select(x => x.HousemateId == housemateId ? x with { Text = text } : x)
                .ToList();
        }
        else
        {
            // New comment: look up housemate details from attendance.
            var housemate = dayPlan.Attendance.FirstOrDefault(x => x.HousemateId == housemateId);
            var newComment = new CommentDto(
                housemateId,
                housemate?.HousemateName ?? "",
                housemate?.Color ?? "",
                text,
                DateTimeOffset.UtcNow);
            updatedComments = dayPlan.Comments.Append(newComment).ToList();
        }

        await SaveDayPlanUpdateAsync(householdId, date, dayPlan with { Comments = updatedComments });
    }

    private async Task ApplyCommentDeleteOptimisticUpdate(string householdId, string date, Guid housemateId)
    {
        var dayPlan = await GetCachedDayPlanAsync(householdId, date);
        if (dayPlan is null)
            return;

        var updatedComments = dayPlan.Comments
            .Where(x => x.HousemateId != housemateId)
            .ToList();

        await SaveDayPlanUpdateAsync(householdId, date, dayPlan with { Comments = updatedComments });
    }

    private async Task ClearSessionAndRedirectAsync(string householdId)
    {
        // Clear cache and mutation queue from IndexedDB.
        await _cacheStore.ClearAllAsync(householdId);

        // Save the current page URL so the user is redirected back after re-login.
        await SaveReturnUrlAsync();

        // Clear session from localStorage.
        await _sessionService.ClearSessionTokensAsync();

        // Use a small delay to ensure IndexedDB writes are flushed before the page reload kills pending operations.
        await Task.Delay(100);

        _navigationManager.NavigateTo("/", forceLoad: true);
    }

    private async Task<DayPlanResponse?> GetCachedDayPlanAsync(string householdId, string date)
    {
        var cached = await _cacheStore.GetDayPlanAsync(householdId, date);
        if (cached is null)
            return null;

        return JsonSerializer.Deserialize<DayPlanResponse>(cached.ResponseJson);
    }

    private async Task SaveDayPlanUpdateAsync(string householdId, string date, DayPlanResponse updatedDayPlan)
    {
        var updatedJson = JsonSerializer.Serialize(updatedDayPlan);
        await _cacheStore.PutDayPlanAsync(householdId, date, updatedJson);
    }

    private async Task<Guid?> GetActiveHousemateIdAsync()
    {
        var value = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "activeHousemateId");
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }

    private async Task<string?> GetHouseholdIdAsync()
    {
        return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "householdId");
    }

    private async Task RedirectToLoginAsync()
    {
        await SaveReturnUrlAsync();

        // Clear session tokens so LoginPage does not auto-redirect back (which would create a loop).
        await _sessionService.ClearSessionTokensAsync();

        _navigationManager.NavigateTo("/", forceLoad: true);
    }

    private async Task SaveReturnUrlAsync()
    {
        var currentUri = _navigationManager.ToBaseRelativePath(_navigationManager.Uri);
        if (!string.IsNullOrWhiteSpace(currentUri))
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "returnUrl", "/" + currentUri);
    }

    private async Task<Dictionary<string, string>> BuildMutationHeadersAsync()
    {
        var headers = new Dictionary<string, string>();

        var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "jwt");
        if (!string.IsNullOrWhiteSpace(token))
            headers["Authorization"] = $"Bearer {token}";

        var activeHousemateId = await GetActiveHousemateIdAsync();
        if (activeHousemateId is not null)
            headers["X-Housemate-Id"] = activeHousemateId.Value.ToString();

        return headers;
    }

    private async Task EnqueueMutationAsync(string householdId, string method, string url, string? body, string date, string mutationType)
    {
        var headers = await BuildMutationHeadersAsync();
        var mutation = new QueuedMutation(
            0, householdId, method, url, headers, body,
            DateTimeOffset.UtcNow, DateOnly.ParseExact(date, "yyyy-MM-dd"), mutationType);

        await _mutationQueue.EnqueueAsync(householdId, mutation);
    }
}
