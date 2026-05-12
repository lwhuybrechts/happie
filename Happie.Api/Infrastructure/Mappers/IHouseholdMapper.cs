using Happie.Api.Infrastructure.Entities;
using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="HouseholdEntity"/> and <see cref="Household"/>.</summary>
public interface IHouseholdMapper
{
    /// <summary>Maps a <see cref="HouseholdEntity"/> to a <see cref="Household"/> domain record.</summary>
    Household ToModel(Guid householdId, HouseholdEntity entity);

    /// <summary>Maps a <see cref="Household"/> domain record to a <see cref="HouseholdEntity"/>.</summary>
    HouseholdEntity ToEntity(Household household);
}
