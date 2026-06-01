using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Request body for toggling chef status on a given day.</summary>
public record UpdateChefStatusRequest(
    [property: JsonPropertyName("isChef")] bool IsChef);
