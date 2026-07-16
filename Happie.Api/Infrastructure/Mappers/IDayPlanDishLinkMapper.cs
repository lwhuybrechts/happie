using Happie.Api.Infrastructure.Entities;
using Happie.Api.Domain;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="DayPlanDishLinkEntity"/> and <see cref="DayPlanDishLink"/>.</summary>
public interface IDayPlanDishLinkMapper
{
    /// <summary>Maps a <see cref="DayPlanDishLinkEntity"/> to a <see cref="DayPlanDishLink"/> domain record.</summary>
    DayPlanDishLink ToModel(DayPlanDishLinkEntity entity);

    /// <summary>Maps a <see cref="DayPlanDishLink"/> domain record to a <see cref="DayPlanDishLinkEntity"/>.</summary>
    DayPlanDishLinkEntity ToEntity(DayPlanDishLink link);
}
