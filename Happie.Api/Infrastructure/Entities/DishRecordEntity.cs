using Happie.Api.Infrastructure.Repositories;

namespace Happie.Api.Infrastructure.Entities;

/// <summary>Azure Table Storage entity representing the planned dish for a specific day in a household.</summary>
public class DishRecordEntity : MyTableEntity
{
    /// <summary>Parameterless constructor required for Azure Table Storage deserialization.</summary>
    public DishRecordEntity() { }

    /// <summary>Initializes a new instance with the standard partition and row key for a dish record.</summary>
    public DishRecordEntity(Guid householdId, DateOnly date)
    {
        PartitionKey = householdId.ToString();
        RowKey = $"{date:yyyy-MM-dd}";
    }

    /// <summary>The description of the planned dish, max 100 characters.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>The ID of the housemate who last changed this dish record.</summary>
    public Guid LastChangedByHousemateId { get; set; }

    /// <summary>The timestamp when this dish record was last changed.</summary>
    public DateTimeOffset? LastChangedAt { get; set; }
}
