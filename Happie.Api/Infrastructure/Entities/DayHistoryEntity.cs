using Happie.Api.Infrastructure.Repositories;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Entities;

/// <summary>Azure Table Storage entity representing an audit log entry for a day plan change.</summary>
public class DayHistoryEntity : MyTableEntity
{
    /// <summary>Parameterless constructor required for Azure Table Storage deserialization.</summary>
    public DayHistoryEntity() { }

    /// <summary>
    /// Initializes a new instance with the standard partition and row key for a day history entry.
    /// The inverted timestamp ensures entries are returned in reverse-chronological order by default.
    /// </summary>
    public DayHistoryEntity(Guid householdId, DateOnly date, DateTimeOffset changedAt)
    {
        PartitionKey = householdId.ToString();
        var invertedTicks = DateTimeOffset.MaxValue.Ticks - changedAt.Ticks;
        RowKey = $"{date:yyyy-MM-dd}_{invertedTicks}";
    }

    /// <summary>The timestamp when this change was made.</summary>
    public DateTimeOffset ChangedAt { get; set; }

    /// <summary>The ID of the housemate who made the change.</summary>
    public Guid ChangedByHousemateId { get; set; }

    /// <summary>The type of change that was recorded.</summary>
    public ChangeType ChangeType { get; set; }

    /// <summary>The translation key identifier for resolving the localized description.</summary>
    public string TranslationKey { get; set; } = string.Empty;

    /// <summary>JSON-serialized dictionary of named string values for template parameter substitution.</summary>
    public string Parameters { get; set; } = string.Empty;
}
