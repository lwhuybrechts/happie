using Happie.Shared.Contracts;

namespace Happie.Web.Services.Caching;

/// <summary>Result of a calendar fetch: cached data (if available) plus an optional background refresh task.</summary>
public record CalendarFetchResult(
    CalendarResponse? Data,
    bool IsColdCacheFetch,
    bool HasLoadError,
    Task? BackgroundRefreshTask);
