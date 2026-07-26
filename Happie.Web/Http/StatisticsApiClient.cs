using System.Net;
using System.Net.Http.Json;
using Happie.Shared.Contracts;

namespace Happie.Web.Http;

/// <summary>HTTP client for fetching dish and housemate statistics from the API.</summary>
public class StatisticsApiClient : IStatisticsApiClient
{
    private readonly HttpClient _httpClient;

    public StatisticsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<DishStatisticsResponse?> GetDishStatisticsAsync(
        Guid dishId,
        DateOnly from,
        DateOnly to)
    {
        var url = $"saved-dishes/{dishId}/statistics?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
        return await GetAsync<DishStatisticsResponse>(url);
    }

    /// <inheritdoc />
    public async Task<DishTimelineResponse?> GetDishTimelineAsync(
        Guid dishId,
        DateOnly from,
        DateOnly to)
    {
        var url = $"saved-dishes/{dishId}/timeline?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
        return await GetAsync<DishTimelineResponse>(url);
    }

    /// <inheritdoc />
    public async Task<HousemateStatisticsResponse?> GetHousemateStatisticsAsync(
        Guid housemateId,
        DateOnly from,
        DateOnly to)
    {
        var url = $"housemates/{housemateId}/statistics?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
        return await GetAsync<HousemateStatisticsResponse>(url);
    }

    /// <inheritdoc />
    public async Task<HousemateTimelineResponse?> GetHousemateTimelineAsync(
        Guid housemateId,
        DateOnly from,
        DateOnly to)
    {
        var url = $"housemates/{housemateId}/timeline?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
        return await GetAsync<HousemateTimelineResponse>(url);
    }

    private async Task<T?> GetAsync<T>(string url) where T : class
    {
        var response = await _httpClient.GetAsync(url);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new HttpRequestException($"Bad request when calling {url}", null, HttpStatusCode.BadRequest);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>();
    }
}
