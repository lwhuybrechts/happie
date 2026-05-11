using Happie.Api.Repositories.Entities;
using Happie.Shared.Domain;

namespace Happie.Api.Repositories.Mappers;

/// <summary>Maps between <see cref="DishRecordEntity"/> and <see cref="DishRecord"/>.</summary>
public interface IDishRecordMapper
{
    /// <summary>Maps a <see cref="DishRecordEntity"/> to a <see cref="DishRecord"/> domain record.</summary>
    DishRecord ToModel(Guid householdId, DateOnly date, DishRecordEntity entity);

    /// <summary>Maps a <see cref="DishRecord"/> domain record to a <see cref="DishRecordEntity"/>.</summary>
    DishRecordEntity ToEntity(DishRecord record, Guid lastChangedByHousemateId);
}
