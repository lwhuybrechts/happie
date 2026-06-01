using Happie.Api.Infrastructure.Entities;
using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="AttendanceRecordEntity"/> and <see cref="AttendanceRecord"/>.</summary>
public class AttendanceRecordMapper : IAttendanceRecordMapper
{
    /// <inheritdoc/>
    public AttendanceRecord ToModel(Guid householdId, AttendanceRecordEntity entity)
    {
        // Row key format: "YYYY-MM-DD_HousemateId".
        var date = DateOnly.Parse(entity.RowKey[..10]);
        return new AttendanceRecord(householdId, entity.HousemateId, date, entity.Status, entity.IsChef);
    }

    /// <inheritdoc/>
    public AttendanceRecordEntity ToEntity(AttendanceRecord record)
    {
        var entity = new AttendanceRecordEntity(record.HouseholdId, record.Date, record.HousemateId);
        entity.HousemateId = record.HousemateId;
        entity.Status = record.Status;
        entity.IsChef = record.IsChef;
        return entity;
    }
}
