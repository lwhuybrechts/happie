using Happie.Api.Repositories.Entities;
using Happie.Shared.Domain;

namespace Happie.Api.Repositories.Mappers;

/// <summary>Maps between <see cref="DayHistoryEntity"/> and <see cref="DayHistoryEntry"/>.</summary>
public class DayHistoryEntryMapper : IDayHistoryEntryMapper
{
    /// <inheritdoc/>
    public DayHistoryEntry ToModel(Guid householdId, DateOnly date, DayHistoryEntity entity) =>
        new(householdId, date, entity.ChangedAt, entity.ChangedByHousemateId, entity.ChangeType, entity.Description);

    /// <inheritdoc/>
    public DayHistoryEntity ToEntity(DayHistoryEntry entry)
    {
        var entity = new DayHistoryEntity(entry.HouseholdId, entry.Date, entry.ChangedAt);
        entity.ChangedAt = entry.ChangedAt;
        entity.ChangedByHousemateId = entry.ChangedByHousemateId;
        entity.ChangeType = entry.ChangeType;
        entity.Description = entry.Description;
        return entity;
    }
}
