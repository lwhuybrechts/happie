using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Request body for the login endpoint.</summary>
public record LoginRequest(
    [property: JsonPropertyName("password")] string Password);
