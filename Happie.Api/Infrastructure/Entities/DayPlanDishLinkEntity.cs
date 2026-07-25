namespace Happie.Api.Infrastructure.Entities;

/// <summary>Azure Table Storage entity representing a link between a day plan and a saved dish.</summary>
public class DayPlanDishLinkEntity : MyTableEntity
{
    /// <summary>Parameterless constructor required for Azure Table Storage deserialization.</summary>
    public DayPlanDishLinkEntity() { }

    /// <summary>Initializes with PK={HouseholdId} and RK={Date}_{SavedDishId}.</summary>
    public DayPlanDishLinkEntity(Guid householdId, DateOnly date, Guid savedDishId)
    {
        PartitionKey = householdId.ToString();
        RowKey = $"{date:yyyy-MM-dd}_{savedDishId}";
    }

    /// <summary>0-based sort order representing the order the dish was selected.</summary>
    public int SortOrder { get; set; }
}
