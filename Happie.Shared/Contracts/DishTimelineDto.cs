using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>A housemate's cooking day entries in the dish timeline chart.</summary>
public record DishTimelineDto(
    [property: JsonPropertyName("housemateId")] Guid HousemateId,
    [property: JsonPropertyName("housemateName")] string HousemateName,
    [property: JsonPropertyName("housemateColor")] string HousemateColor,
    [property: JsonPropertyName("sortOrder")] int SortOrder,
    [property: JsonPropertyName("cookingDays")] IReadOnlyList<string> CookingDays);
