namespace Happie.Web.Services.Caching;

/// <summary>Represents a cached Calendar response stored in IndexedDB.</summary>
public record CachedCalendar(
    string Month,
    string ResponseJson,
    long Timestamp);
