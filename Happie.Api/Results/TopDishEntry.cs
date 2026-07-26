namespace Happie.Api.Results;

public record TopDishEntry(
    Guid SavedDishId,
    string Description,
    int Count);
