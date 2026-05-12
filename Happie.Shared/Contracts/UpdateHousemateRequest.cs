using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Request body for the update housemate endpoint.</summary>
public record UpdateHousemateRequest(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("color")] string? Color);
