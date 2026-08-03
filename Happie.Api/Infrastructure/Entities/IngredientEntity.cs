using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Entities;

/// <summary>Azure Table Storage entity representing a single ingredient in a recipe.</summary>
public class IngredientEntity : MyTableEntity
{
    /// <summary>Parameterless constructor required for Azure Table Storage deserialization.</summary>
    public IngredientEntity() { }

    /// <summary>Initializes a new instance with the standard partition and row key for an ingredient.</summary>
    public IngredientEntity(Guid householdId, Guid savedDishId, Guid ingredientId)
    {
        PartitionKey = householdId.ToString();
        RowKey = $"{savedDishId}_{ingredientId}";
    }

    /// <summary>The quantity of this ingredient.</summary>
    public double Amount { get; set; }

    /// <summary>The unit of measurement for this ingredient.</summary>
    public UnitOfMeasurement Unit { get; set; }

    /// <summary>The display name of this ingredient.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The sort order for display purposes.</summary>
    public int SortOrder { get; set; }
}
