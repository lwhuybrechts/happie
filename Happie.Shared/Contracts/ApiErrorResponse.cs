using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Standard error response body returned by all API endpoints.</summary>
public record ApiErrorResponse(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("code")] string Code);
