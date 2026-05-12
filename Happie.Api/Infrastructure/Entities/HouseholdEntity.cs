using Happie.Api.Infrastructure.Repositories;

namespace Happie.Api.Infrastructure.Entities;

/// <summary>Azure Table Storage entity representing a household.</summary>
public class HouseholdEntity : MyTableEntity
{
    /// <summary>Parameterless constructor required for Azure Table Storage deserialization.</summary>
    public HouseholdEntity() { }

    /// <summary>Initializes a new instance with the standard partition and row key for a household.</summary>
    public HouseholdEntity(Guid householdId)
    {
        PartitionKey = "households";
        RowKey = householdId.ToString();
    }

    /// <summary>The display name of the household.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The bcrypt hash of the household password.</summary>
    public string PasswordHash { get; set; } = string.Empty;
}
