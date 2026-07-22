using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Happie.Shared.Validation;

namespace Happie.Shared.Contracts;

/// <summary>Request body for updating a saved dish description.</summary>
public record UpdateSavedDishRequest(
    [property: JsonPropertyName("description")]
    [property: Required(ErrorMessage = "Description is required.")]
    [property: MaxLength(100, ErrorMessage = "Description must be at most 100 characters.")]
    [property: NoAmpersand(ErrorMessage = "Description must not contain the '&' character.")]
    string Description);
