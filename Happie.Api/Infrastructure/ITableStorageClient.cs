namespace Happie.Api.Infrastructure;

/// <summary>Typed wrapper around Azure Table Storage for common CRUD and query operations.</summary>
public interface ITableStorageClient
{
    /// <summary>Upserts an entity (insert or replace).</summary>
    Task UpsertAsync<T>(string tableName, T entity, CancellationToken cancellationToken = default)
        where T : MyTableEntity;

    /// <summary>Gets a single entity by partition key and row key, or null if not found.</summary>
    Task<T?> GetAsync<T>(string tableName, string partitionKey, string rowKey, CancellationToken cancellationToken = default)
        where T : MyTableEntity;

    /// <summary>Deletes an entity by partition key and row key. No-ops if the entity does not exist.</summary>
    Task DeleteAsync(string tableName, string partitionKey, string rowKey, CancellationToken cancellationToken = default);

    /// <summary>Queries all entities in a partition.</summary>
    Task<IReadOnlyList<T>> QueryByPartitionAsync<T>(string tableName, string partitionKey, CancellationToken cancellationToken = default)
        where T : MyTableEntity;

    /// <summary>Queries entities whose row key starts with the given prefix within a partition.</summary>
    Task<IReadOnlyList<T>> QueryByRowKeyPrefixAsync<T>(string tableName, string partitionKey, string rowKeyPrefix, CancellationToken cancellationToken = default)
        where T : MyTableEntity;
}
