using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Statistics for a saved dish, as returned by GET /api/saved-dishes/{id}/statistics.</summary>
public record DishStatisticsResponse(
    [property: JsonPropertyName("timesCooked")] int TimesCooked,
    [property: JsonPropertyName("allTimeTimesCooked")] int AllTimeTimesCooked,
    [property: JsonPropertyName("lastCookedDate")] string? LastCookedDate,
    [property: JsonPropertyName("firstCookedDate")] string? FirstCookedDate,
    [property: JsonPropertyName("cookingShares")] IReadOnlyList<CookingShareDto> CookingShares);
