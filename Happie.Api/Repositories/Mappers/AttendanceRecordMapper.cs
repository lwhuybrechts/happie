using Happie.Api.Repositories.Entities;
using Happie.Shared.Domain;

namespace Happie.Api.Repositories.Mappers;

/// <summary>Maps between <see cref="AttendanceRecordEntity"/> and <see cref="AttendanceRecord"/>.</summary>
public class AttendanceRecordMapper : IAttendanceRecordMapper
{
    /// <inheritdoc/>
    public AttendanceRecord ToModel(Guid householdId, AttendanceRecordEntity entity)
    {
        // Row key format: "YYYY-MM-DD#HousemateId".
        var date = DateOnly.Parse(entity.RowKey[..10]);
        return new AttendanceRecord(householdId, entity.HousemateId, date, entity.Status);
    }

    /// <inheritdoc/>
    public AttendanceRecordEntity ToEntity(AttendanceRecord record)
    {
        var entity = new AttendanceRecordEntity(record.HouseholdId, record.Date, record.HousemateId);
        entity.HousemateId = record.HousemateId;
        entity.Status = record.Status;
        return entity;
    }
}
