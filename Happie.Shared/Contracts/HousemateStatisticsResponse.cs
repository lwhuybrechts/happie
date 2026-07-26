using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Response for the housemate statistics endpoint.</summary>
public record HousemateStatisticsResponse(
    [property: JsonPropertyName("timesCooked")] int TimesCooked,
    [property: JsonPropertyName("allTimeTimesCooked")] int AllTimeTimesCooked,
    [property: JsonPropertyName("daysEatingIn")] int DaysEatingIn,
    [property: JsonPropertyName("cookRatioDays")] int CookRatioDays,
    [property: JsonPropertyName("cookRatioEatingInDays")] int CookRatioEatingInDays,
    [property: JsonPropertyName("longestStreak")] int LongestStreak,
    [property: JsonPropertyName("busiestWeek")] int BusiestWeek,
    [property: JsonPropertyName("cookingShares")] IReadOnlyList<CookingShareDto> CookingShares,
    [property: JsonPropertyName("topDishes")] IReadOnlyList<TopDishDto> TopDishes);
