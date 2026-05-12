using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Request body for the upsert comment endpoint.</summary>
public record UpdateCommentRequest(
    [property: JsonPropertyName("text")]
    [property: MaxLength(200, ErrorMessage = "Comment must be at most 200 characters.")]
    string Text);
