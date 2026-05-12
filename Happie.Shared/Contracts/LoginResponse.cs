using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Response body for a successful login.</summary>
public record LoginResponse(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("housemates")] IReadOnlyList<HousemateDto> Housemates);
