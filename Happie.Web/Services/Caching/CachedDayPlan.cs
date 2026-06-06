namespace Happie.Web.Services.Caching;

/// <summary>Represents a cached DayPlan response stored in IndexedDB.</summary>
public record CachedDayPlan(
    string Date,
    string ResponseJson,
    long Timestamp);
