using Happie.Api.Infrastructure.Entities;
using Happie.Api.Domain;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="DayPlanDishLinkEntity"/> and <see cref="DayPlanDishLink"/>.</summary>
public class DayPlanDishLinkMapper : IDayPlanDishLinkMapper
{
    /// <inheritdoc/>
    public DayPlanDishLink ToModel(DayPlanDishLinkEntity entity)
    {
        // PK format: "{HouseholdId}_{YYYY-MM-DD}".
        var parts = entity.PartitionKey.Split('_', 2);
        var householdId = Guid.Parse(parts[0]);
        var date = DateOnly.ParseExact(parts[1], "yyyy-MM-dd");
        var savedDishId = Guid.Parse(entity.RowKey);
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
