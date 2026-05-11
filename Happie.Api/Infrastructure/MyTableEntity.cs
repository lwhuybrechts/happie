using Azure;
using Azure.Data.Tables;

namespace Happie.Api.Infrastructure;

/// <summary>Base class for all Azure Table Storage entities in Happie.</summary>
public abstract class MyTableEntity : ITableEntity
{
    /// <summary>The partition key for the entity.</summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>The row key for the entity.</summary>
    public string RowKey { get; set; } = string.Empty;

    /// <summary>The timestamp of the entity.</summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>The ETag of the entity.</summary>
    public ETag ETag { get; set; }
}
