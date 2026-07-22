namespace Happie.Web.Services.Caching;

/// <summary>Represents a cached saved dishes response stored in IndexedDB.</summary>
public record CachedSavedDishes(
    string ResponseJson,
    long Timestamp);
