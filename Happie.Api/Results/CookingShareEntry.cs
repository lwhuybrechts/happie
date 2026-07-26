namespace Happie.Api.Results;

public record CookingShareEntry(
    Guid HousemateId,
    string HousemateName,
    string HousemateColor,
    int ChefDayCount);
