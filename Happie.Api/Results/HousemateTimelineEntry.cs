namespace Happie.Api.Results;

public record HousemateTimelineEntry(
    Guid SavedDishId,
    string DishDescription,
    int AllTimeFrequency,
    IReadOnlyList<DateOnly> CookingDays);
