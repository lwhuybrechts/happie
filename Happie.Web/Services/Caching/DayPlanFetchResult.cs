using Happie.Shared.Contracts;

namespace Happie.Web.Services.Caching;

/// <summary>Result of a day plan fetch: cached data (if available) plus an optional background refresh task.</summary>
public record DayPlanFetchResult(
    DayPlanResponse? Data,
    bool IsColdCacheFetch,
    bool HasLoadError,
    Task? BackgroundRefreshTask);
