using Happie.Api.Infrastructure.Entities;
using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="HouseholdEntity"/> and <see cref="Household"/>.</summary>
public class HouseholdMapper : IHouseholdMapper
{
    /// <inheritdoc/>
    public Household ToModel(Guid householdId, HouseholdEntity entity) =>
        new(householdId, entity.Name, entity.PasswordHash);

    /// <inheritdoc/>
    public HouseholdEntity ToEntity(Household household)
    {
        var entity = new HouseholdEntity(household.Id);
        entity.Name = household.Name;
        entity.PasswordHash = household.PasswordHash;
        return entity;
    }
}
