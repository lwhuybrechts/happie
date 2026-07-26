using Happie.Api.Results;

namespace Happie.Api.Handlers;

/// <summary>Handles housemate statistics computation.</summary>
public interface IHousemateStatisticsHandler
{
    /// <summary>
    /// Computes statistics for a housemate within the specified date range.
    /// Returns times cooked, days eating in, cook ratio, longest streak, busiest week,
    /// cooking shares, and top dishes.
    /// </summary>
    Task<HousemateStatisticsResult> GetStatisticsAsync(
        Guid householdId,
        Guid housemateId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes timeline entries for a housemate within the specified timeline window.
    /// Returns per-dish cooking day dots and the earliest date the housemate cooked.
    /// </summary>
    Task<HousemateTimelineResult> GetTimelineAsync(
        Guid householdId,
        Guid housemateId,
        DateOnly timelineFrom,
        DateOnly timelineTo,
        CancellationToken cancellationToken = default);
}
