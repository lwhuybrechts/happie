using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Request body for PUT /api/saved-dishes/{id}/instructions.</summary>
public record UpdateInstructionsRequest(
    [property: JsonPropertyName("instructions")] IReadOnlyList<CookingInstructionDto> Instructions);
