using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Entities;

/// <summary>Azure Table Storage entity representing a housemate's attendance for a specific day.</summary>
public class AttendanceRecordEntity : MyTableEntity
{
    /// <summary>Parameterless constructor required for Azure Table Storage deserialization.</summary>
    public AttendanceRecordEntity() { }

    /// <summary>Initializes a new instance with the standard partition and row key for an attendance record.</summary>
    public AttendanceRecordEntity(Guid householdId, DateOnly date, Guid housemateId)
    {
        PartitionKey = householdId.ToString();
        RowKey = $"{date:yyyy-MM-dd}_{housemateId}";
    }

    /// <summary>The ID of the housemate this record belongs to.</summary>
    public Guid HousemateId { get; set; }

    /// <summary>The attendance status of the housemate for this day.</summary>
    public AttendanceStatus Status { get; set; }
}
