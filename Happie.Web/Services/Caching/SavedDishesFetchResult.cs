using Happie.Shared.Contracts;

namespace Happie.Web.Services.Caching;

/// <summary>Result of a saved dishes fetch: cached data (if available), cold cache indicator, and error flag.</summary>
public record SavedDishesFetchResult(
    IReadOnlyList<SavedDishDto>? Dishes,
    bool IsColdCache,
    bool HasError);
