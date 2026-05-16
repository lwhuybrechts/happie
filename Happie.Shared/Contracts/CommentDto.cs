using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>A housemate's comment for a specific day, as returned in the day plan response.</summary>
public record CommentDto(
    [property: JsonPropertyName("housemateId")] Guid HousemateId,
    [property: JsonPropertyName("housemateName")] string HousemateName,
    [property: JsonPropertyName("color")] string Color,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("lastEditedAt")] DateTimeOffset? LastEditedAt);
