using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Request body for creating a new saved dish.</summary>
public record CreateSavedDishRequest(
    [property: JsonPropertyName("description")]
    [property: Required(ErrorMessage = "Description is required.")]
    [property: MaxLength(100, ErrorMessage = "Description must be at most 100 characters.")]
    string Description);
