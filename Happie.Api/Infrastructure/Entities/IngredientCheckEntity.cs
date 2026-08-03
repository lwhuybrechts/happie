namespace Happie.Api.Infrastructure.Entities;

/// <summary>Azure Table Storage entity representing the checked state of an ingredient.</summary>
public class IngredientCheckEntity : MyTableEntity
{
    /// <summary>Parameterless constructor required for Azure Table Storage deserialization.</summary>
    public IngredientCheckEntity() { }

    /// <summary>Initializes a new instance with the standard partition and row key for an ingredient check.</summary>
    public IngredientCheckEntity(Guid householdId, Guid savedDishId, Guid ingredientId)
    {
        PartitionKey = householdId.ToString();
        RowKey = $"{savedDishId}_{ingredientId}";
    }

    /// <summary>Whether this ingredient has been checked off.</summary>
    public bool IsChecked { get; set; }
}
