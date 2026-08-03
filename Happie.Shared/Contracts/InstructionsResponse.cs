using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Response returned by GET /api/saved-dishes/{id}/instructions.</summary>
public record InstructionsResponse(
    [property: JsonPropertyName("instructions")] IReadOnlyList<CookingInstructionDto> Instructions);
