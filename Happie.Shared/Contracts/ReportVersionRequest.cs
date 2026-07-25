using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Request body for the report version endpoint.</summary>
public record ReportVersionRequest(
    [property: JsonPropertyName("version")]
    [property: Required(ErrorMessage = "Version is required.")]
    [property: MaxLength(20, ErrorMessage = "Version must be at most 20 characters.")]
    string? Version);
