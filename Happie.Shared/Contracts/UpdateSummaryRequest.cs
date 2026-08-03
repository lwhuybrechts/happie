using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Request body for PUT /api/saved-dishes/{id}/summary.</summary>
public record UpdateSummaryRequest(
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("cookingDurationMinutes")] int? CookingDurationMinutes,
    [property: JsonPropertyName("servings")] int? Servings);
