using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>A dish row in the housemate timeline chart.</summary>
public record HousemateTimelineDto(
    [property: JsonPropertyName("savedDishId")] Guid SavedDishId,
    [property: JsonPropertyName("dishDescription")] string DishDescription,
    [property: JsonPropertyName("allTimeFrequency")] int AllTimeFrequency,
    [property: JsonPropertyName("cookingDays")] IReadOnlyList<string> CookingDays);
