namespace Happie.Api.Infrastructure.Entities;

/// <summary>Azure Table Storage entity representing a single cooking instruction step.</summary>
public class CookingInstructionEntity : MyTableEntity
{
    /// <summary>Parameterless constructor required for Azure Table Storage deserialization.</summary>
    public CookingInstructionEntity() { }

    /// <summary>Initializes a new instance with the standard partition and row key for a cooking instruction.</summary>
    public CookingInstructionEntity(Guid householdId, Guid savedDishId, Guid instructionId)
    {
        PartitionKey = householdId.ToString();
        RowKey = $"{savedDishId}_{instructionId}";
    }

    /// <summary>The instruction text for this step.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>The sort order for display purposes.</summary>
    public int SortOrder { get; set; }
}
