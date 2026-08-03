namespace Happie.Api.Infrastructure.Entities;

/// <summary>Azure Table Storage entity representing a recipe summary for a saved dish.</summary>
public class RecipeSummaryEntity : MyTableEntity
{
    /// <summary>Parameterless constructor required for Azure Table Storage deserialization.</summary>
    public RecipeSummaryEntity() { }

    /// <summary>Initializes a new instance with the standard partition and row key for a recipe summary.</summary>
    public RecipeSummaryEntity(Guid householdId, Guid savedDishId)
    {
        PartitionKey = householdId.ToString();
        RowKey = savedDishId.ToString();
    }

    /// <summary>The summary text describing the recipe.</summary>
    public string? Summary { get; set; }

    /// <summary>The estimated cooking duration in minutes.</summary>
    public int? CookingDurationMinutes { get; set; }

    /// <summary>The number of servings this recipe produces.</summary>
    public int? Servings { get; set; }
}
