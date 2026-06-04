using Happie.Api.Infrastructure.Entities;
using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="DishRecordEntity"/> and <see cref="DishRecord"/>.</summary>
public class DishRecordMapper : IDishRecordMapper
{
    /// <inheritdoc/>
    public DishRecord ToModel(Guid householdId, DateOnly date, DishRecordEntity entity) =>
        new(
            householdId,
            date,
            entity.Description,
            entity.LastChangedByHousemateId == Guid.Empty ? null : entity.LastChangedByHousemateId,
            entity.LastChangedAt == default ? null : entity.LastChangedAt,
            entity.DinnerTimeHour == -1 || entity.DinnerTimeMinute == -1
                ? null
                : new TimeOnly(entity.DinnerTimeHour, entity.DinnerTimeMinute));

    /// <inheritdoc/>
    public DishRecordEntity ToEntity(DishRecord record)
    {
        var entity = new DishRecordEntity(record.HouseholdId, record.Date);
        entity.Description = record.Description;
        entity.LastChangedByHousemateId = record.LastChangedByHousemateId ?? Guid.Empty;
        entity.LastChangedAt = record.LastChangedAt ?? default;
        entity.DinnerTimeHour = record.DinnerTime?.Hour ?? -1;
        entity.DinnerTimeMinute = record.DinnerTime?.Minute ?? -1;
        return entity;
    }
}
