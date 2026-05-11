using Happie.Api.Repositories.Entities;
using Happie.Shared.Domain;

namespace Happie.Api.Repositories.Mappers;

/// <summary>Maps between <see cref="DayHistoryEntity"/> and <see cref="DayHistoryEntry"/>.</summary>
public interface IDayHistoryEntryMapper
{
    /// <summary>Maps a <see cref="DayHistoryEntity"/> to a <see cref="DayHistoryEntry"/> domain record.</summary>
    DayHistoryEntry ToModel(Guid householdId, DateOnly date, DayHistoryEntity entity);

    /// <summary>Maps a <see cref="DayHistoryEntry"/> domain record to a <see cref="DayHistoryEntity"/>.</summary>
    DayHistoryEntity ToEntity(DayHistoryEntry entry);
}
