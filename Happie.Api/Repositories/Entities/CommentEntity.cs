using Happie.Api.Infrastructure;

namespace Happie.Api.Repositories.Entities;

/// <summary>Azure Table Storage entity representing a housemate's comment for a specific day.</summary>
public class CommentEntity : MyTableEntity
{
    /// <summary>Parameterless constructor required for Azure Table Storage deserialization.</summary>
    public CommentEntity() { }

    /// <summary>Initializes a new instance with the standard partition and row key for a comment.</summary>
    public CommentEntity(Guid householdId, DateOnly date, Guid housemateId)
    {
        PartitionKey = householdId.ToString();
        RowKey = $"{date:yyyy-MM-dd}#{housemateId}";
    }

    /// <summary>The comment text, max 200 characters.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>The ID of the housemate who authored this comment.</summary>
    public Guid HousemateId { get; set; }
}
