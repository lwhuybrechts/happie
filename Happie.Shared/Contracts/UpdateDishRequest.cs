using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Request body for the update dish endpoint.</summary>
public record UpdateDishRequest(
    [property: JsonPropertyName("description")] string Description);
