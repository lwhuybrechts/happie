using Happie.Api.Infrastructure.Entities;
using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="HousemateEntity"/> and <see cref="Housemate"/>.</summary>
public class HousemateMapper : IHousemateMapper
{
    /// <inheritdoc/>
    public Housemate ToModel(Guid householdId, HousemateEntity entity) =>
        new(Guid.Parse(entity.RowKey), householdId, entity.Name, entity.Color, entity.IsDeleted);

    /// <inheritdoc/>
    public HousemateEntity ToEntity(Housemate housemate)
    {
        var entity = new HousemateEntity(housemate.HouseholdId, housemate.Id);
        entity.Name = housemate.Name;
        entity.Color = housemate.Color;
        entity.IsDeleted = housemate.IsDeleted;
        return entity;
    }
}
