using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>A housemate's cooking share within a date range.</summary>
public record CookingShareDto(
    [property: JsonPropertyName("housemateId")] Guid HousemateId,
    [property: JsonPropertyName("housemateName")] string HousemateName,
    [property: JsonPropertyName("housemateColor")] string HousemateColor,
    [property: JsonPropertyName("chefDayCount")] int ChefDayCount);
