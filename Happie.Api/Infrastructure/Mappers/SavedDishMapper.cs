using Happie.Api.Domain;
using Happie.Api.Infrastructure.Entities;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="SavedDishEntity"/> and <see cref="SavedDish"/>.</summary>
public class SavedDishMapper : ISavedDishMapper
{
    /// <inheritdoc/>
    public SavedDish ToModel(Guid householdId, SavedDishEntity entity) =>
        new(Guid.Parse(entity.RowKey), householdId, entity.Description, entity.IsDeleted);

    /// <inheritdoc/>
    public SavedDishEntity ToEntity(SavedDish savedDish)
    {
        var entity = new SavedDishEntity(savedDish.HouseholdId, savedDish.Id);
        entity.Description = savedDish.Description;
        entity.IsDeleted = savedDish.IsDeleted;
        return entity;
    }
}
