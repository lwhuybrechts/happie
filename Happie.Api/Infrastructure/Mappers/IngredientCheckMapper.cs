using Happie.Api.Domain;
using Happie.Api.Infrastructure.Entities;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="IngredientCheckEntity"/> and <see cref="IngredientCheck"/>.</summary>
public class IngredientCheckMapper : IIngredientCheckMapper
{
    /// <inheritdoc/>
    public IngredientCheck ToModel(Guid householdId, IngredientCheckEntity entity)
    {
        var parts = entity.RowKey.Split('_');
        var savedDishId = Guid.Parse(parts[0]);
        var ingredientId = Guid.Parse(parts[1]);
        return new IngredientCheck(householdId, savedDishId, ingredientId, entity.IsChecked);
    }

    /// <inheritdoc/>
    public IngredientCheckEntity ToEntity(IngredientCheck check)
    {
        var entity = new IngredientCheckEntity(check.HouseholdId, check.SavedDishId, check.IngredientId);
        entity.IsChecked = check.IsChecked;
        return entity;
    }
}
