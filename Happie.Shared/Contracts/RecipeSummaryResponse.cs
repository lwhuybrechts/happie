using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Response returned by GET /api/saved-dishes/{id}/summary.</summary>
public record RecipeSummaryResponse(
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("cookingDurationMinutes")] int? CookingDurationMinutes,
    [property: JsonPropertyName("servings")] int? Servings);
