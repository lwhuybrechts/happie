using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Request body for the upsert comment endpoint.</summary>
public record UpdateCommentRequest(
    [property: JsonPropertyName("text")] string Text);
