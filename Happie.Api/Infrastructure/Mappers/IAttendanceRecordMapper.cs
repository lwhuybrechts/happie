using Happie.Api.Infrastructure.Entities;
using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="AttendanceRecordEntity"/> and <see cref="AttendanceRecord"/>.</summary>
public interface IAttendanceRecordMapper
{
    /// <summary>Maps an <see cref="AttendanceRecordEntity"/> to an <see cref="AttendanceRecord"/> domain record.</summary>
    AttendanceRecord ToModel(Guid householdId, AttendanceRecordEntity entity);

    /// <summary>Maps an <see cref="AttendanceRecord"/> domain record to an <see cref="AttendanceRecordEntity"/>.</summary>
    AttendanceRecordEntity ToEntity(AttendanceRecord record);
}
