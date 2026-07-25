using Happie.Api.Infrastructure.Repositories;

namespace Happie.Api.Infrastructure.Entities;

/// <summary>Azure Table Storage entity representing a housemate within a household.</summary>
public class HousemateEntity : MyTableEntity
{
    /// <summary>Parameterless constructor required for Azure Table Storage deserialization.</summary>
    public HousemateEntity() { }

    /// <summary>Initializes a new instance with the standard partition and row key for a housemate.</summary>
    public HousemateEntity(Guid householdId, Guid housemateId)
    {
        PartitionKey = householdId.ToString();
        RowKey = housemateId.ToString();
    }

    /// <summary>The display name of the housemate.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The hex color code from the predefined palette, e.g. "#E91E63".</summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>Indicates whether the housemate has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Sort order for display purposes. Lower values appear first.</summary>
    public int SortOrder { get; set; }

    /// <summary>The last reported app version, or null if never reported.</summary>
    public string? AppVersion { get; set; }
}
