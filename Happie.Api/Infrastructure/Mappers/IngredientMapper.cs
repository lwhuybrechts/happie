using Happie.Api.Domain;
using Happie.Api.Infrastructure.Entities;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="IngredientEntity"/> and <see cref="Ingredient"/>.</summary>
public class IngredientMapper : IIngredientMapper
{
    /// <inheritdoc/>
    public Ingredient ToModel(Guid householdId, IngredientEntity entity)
    {
        var parts = entity.RowKey.Split('_');
        var savedDishId = Guid.Parse(parts[0]);
        var ingredientId = Guid.Parse(parts[1]);
        return new Ingredient(ingredientId, householdId, savedDishId, entity.Amount, entity.Unit, entity.Name, entity.SortOrder);
    }

    /// <inheritdoc/>
    public IngredientEntity ToEntity(Ingredient ingredient)
    {
        var entity = new IngredientEntity(ingredient.HouseholdId, ingredient.SavedDishId, ingredient.Id);
        entity.Amount = ingredient.Amount;
        entity.Unit = ingredient.Unit;
        entity.Name = ingredient.Name;
        entity.SortOrder = ingredient.SortOrder;
        return entity;
    }
}
