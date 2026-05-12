using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Request body for the add housemate endpoint.</summary>
public record AddHousemateRequest(
    [property: JsonPropertyName("name")]
    [property: Required(ErrorMessage = "Name is required.")]
    string? Name);
