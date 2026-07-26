namespace Happie.Api.Results;

public record DishTimelineEntry(
    Guid HousemateId,
    string HousemateName,
    string HousemateColor,
    int SortOrder,
    IReadOnlyList<DateOnly> CookingDays);
