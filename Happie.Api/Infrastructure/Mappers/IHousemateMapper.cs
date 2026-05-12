using Happie.Api.Infrastructure.Entities;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="HousemateEntity"/> and <see cref="Housemate"/>.</summary>
public interface IHousemateMapper
{
    /// <summary>Maps a <see cref="HousemateEntity"/> to a <see cref="Housemate"/> domain record.</summary>
    Housemate ToModel(Guid householdId, HousemateEntity entity);

    /// <summary>Maps a <see cref="Housemate"/> domain record to a <see cref="HousemateEntity"/>.</summary>
    HousemateEntity ToEntity(Housemate housemate);
}
