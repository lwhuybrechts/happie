namespace Happie.Api.Results;

public record DishStatisticsResult(
    int TimesCooked,
    int AllTimeTimesCooked,
    DateOnly? LastCookedDate,
    DateOnly? FirstCookedDate,
    IReadOnlyList<CookingShareEntry> CookingShares);
