using Happie.Shared.Contracts;

namespace Happie.Web.Http;

/// <summary>Client for fetching dish and housemate statistics from the API.</summary>
public interface IStatisticsApiClient
{
    /// <summary>
    /// Fetches statistics for a saved dish. Returns null if the dish was not found (404).
    /// </summary>
    Task<DishStatisticsResponse?> GetDishStatisticsAsync(
        Guid dishId,
        DateOnly from,
        DateOnly to);

    /// <summary>
    /// Fetches timeline data for a saved dish. Returns null if the dish was not found (404).
    /// </summary>
    Task<DishTimelineResponse?> GetDishTimelineAsync(
        Guid dishId,
        DateOnly from,
        DateOnly to);

    /// <summary>
    /// Fetches statistics for a housemate. Returns null if the housemate was not found (404).
    /// </summary>
    Task<HousemateStatisticsResponse?> GetHousemateStatisticsAsync(
        Guid housemateId,
        DateOnly from,
        DateOnly to);

    /// <summary>
    /// Fetches timeline data for a housemate. Returns null if the housemate was not found (404).
    /// </summary>
    Task<HousemateTimelineResponse?> GetHousemateTimelineAsync(
        Guid housemateId,
        DateOnly from,
        DateOnly to);
}
