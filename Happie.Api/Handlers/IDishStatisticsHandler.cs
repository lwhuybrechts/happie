using Happie.Api.Results;

namespace Happie.Api.Handlers;

/// <summary>Handles dish statistics computation.</summary>
public interface IDishStatisticsHandler
{
    /// <summary>
    /// Computes statistics for a saved dish within the specified date range.
    /// Returns times cooked (in-range and all-time) and last cooked date.
    /// </summary>
    Task<DishStatisticsResult> GetStatisticsAsync(
        Guid householdId,
        Guid savedDishId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes timeline entries for a saved dish within the specified timeline window.
    /// Returns per-housemate cooking day dots and the earliest date the dish was ever cooked.
    /// </summary>
    Task<DishTimelineResult> GetTimelineAsync(
        Guid householdId,
        Guid savedDishId,
        DateOnly timelineFrom,
        DateOnly timelineTo,
        CancellationToken cancellationToken = default);
}
