using Happie.Api.Domain;
using Happie.Api.Infrastructure.Entities;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="SavedDishEntity"/> and <see cref="SavedDish"/>.</summary>
public interface ISavedDishMapper
{
    /// <summary>Maps a <see cref="SavedDishEntity"/> to a <see cref="SavedDish"/> domain record.</summary>
    SavedDish ToModel(Guid householdId, SavedDishEntity entity);

    /// <summary>Maps a <see cref="SavedDish"/> domain record to a <see cref="SavedDishEntity"/>.</summary>
    SavedDishEntity ToEntity(SavedDish savedDish);
}
