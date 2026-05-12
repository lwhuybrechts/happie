using System.Text.Json.Serialization;
using Happie.Shared.Domain;

namespace Happie.Shared.Contracts;

/// <summary>An audit log entry for a day plan change, as returned in the day plan response.</summary>
public record HistoryEntryDto(
    [property: JsonPropertyName("changedAt")] DateTimeOffset ChangedAt,
    [property: JsonPropertyName("changedByHousemateName")] string ChangedByHousemateName,
    [property: JsonPropertyName("changeType")] ChangeType ChangeType,
    [property: JsonPropertyName("description")] string Description);
