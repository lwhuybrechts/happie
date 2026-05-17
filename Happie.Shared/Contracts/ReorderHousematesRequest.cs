using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Happie.Shared.Contracts;

/// <summary>Request body for reordering housemates.</summary>
public record ReorderHousematesRequest(
    [property: JsonPropertyName("orderedIds")]
    [property: Required]
    [property: MinLength(1)]
    List<Guid> OrderedIds);
