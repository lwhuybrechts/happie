using Happie.Api.Infrastructure.Entities;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="DishRecordEntity"/> and <see cref="DishRecord"/>.</summary>
public class DishRecordMapper : IDishRecordMapper
{
    /// <inheritdoc/>
    public DishRecord ToModel(Guid householdId, DateOnly date, DishRecordEntity entity) =>
        new(householdId, date, entity.Description);

    /// <inheritdoc/>
    public DishRecordEntity ToEntity(DishRecord record, Guid lastChangedByHousemateId)
    {
        var entity = new DishRecordEntity(record.HouseholdId, record.Date);
        entity.Description = record.Description;
        entity.LastChangedByHousemateId = lastChangedByHousemateId;
        return entity;
    }
}
