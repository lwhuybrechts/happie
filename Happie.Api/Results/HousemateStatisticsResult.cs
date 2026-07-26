namespace Happie.Api.Results;

public record HousemateStatisticsResult(
    int TimesCooked,
    int AllTimeTimesCooked,
    int DaysEatingIn,
    int CookRatioDays,
    int CookRatioEatingInDays,
    int LongestStreak,
    int BusiestWeek,
    IReadOnlyList<CookingShareEntry> CookingShares,
    IReadOnlyList<TopDishEntry> TopDishes);
