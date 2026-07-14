using Happie.Api.Infrastructure.Repositories;

namespace Happie.Api.Infrastructure.Entities;

/// <summary>Azure Table Storage entity representing a saved dish within a household.</summary>
public class SavedDishEntity : MyTableEntity
{
    /// <summary>Parameterless constructor required for Azure Table Storage deserialization.</summary>
    public SavedDishEntity() { }

    /// <summary>Initializes a new instance with the standard partition and row key for a saved dish.</summary>
    public SavedDishEntity(Guid householdId, Guid savedDishId)
    {
        PartitionKey = householdId.ToString();
        RowKey = savedDishId.ToString();
    }

    /// <summary>The description text of the saved dish.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Indicates whether the saved dish has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }
}
