using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Housemate data returned as part of the login response.</summary>
public record HousemateDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("color")] string Color,
    [property: JsonPropertyName("sortOrder")] int SortOrder = 0);
