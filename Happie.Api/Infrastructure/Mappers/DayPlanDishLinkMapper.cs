using Happie.Api.Infrastructure.Entities;
using Happie.Api.Domain;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="DayPlanDishLinkEntity"/> and <see cref="DayPlanDishLink"/>.</summary>
public class DayPlanDishLinkMapper : IDayPlanDishLinkMapper
{
    /// <inheritdoc/>
    public DayPlanDishLink ToModel(DayPlanDishLinkEntity entity)
    {
        var householdId = Guid.Parse(entity.PartitionKey);
        var date = DateOnly.ParseExact(entity.RowKey[..10], "yyyy-MM-dd");
        var savedDishId = Guid.Parse(entity.RowKey[11..]);
        return new DayPlanDishLink(householdId, date, savedDishId, entity.SortOrder);
    }

    /// <inheritdoc/>
    public DayPlanDishLinkEntity ToEntity(DayPlanDishLink link)
    {
        var entity = new DayPlanDishLinkEntity(link.HouseholdId, link.Date, link.SavedDishId);
        entity.SortOrder = link.SortOrder;
        return entity;
    }
}
