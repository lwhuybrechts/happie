using Happie.Api.Infrastructure.Repositories;

namespace Happie.Api.Infrastructure;

/// <summary>Abstract base class providing common Table Storage operations for all repositories.</summary>
public abstract class BaseRepository<TEntity> where TEntity : MyTableEntity
{
    private readonly ITableStorageClient _client;
    private readonly string _tableName;

    /// <summary>Initializes a new instance of <see cref="BaseRepository{TEntity}"/>.</summary>
    protected BaseRepository(ITableStorageClient client, string tableName)
    {
        _client = client;
        _tableName = tableName;
    }

    /// <summary>Upserts the given entity into the table.</summary>
    protected Task UpsertAsync(TEntity entity, CancellationToken ct = default)
        => _client.UpsertAsync(_tableName, entity, ct);

    /// <summary>Gets a single entity by partition key and row key, or null if not found.</summary>
    protected Task<TEntity?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default)
        => _client.GetAsync<TEntity>(_tableName, partitionKey, rowKey, ct);

    /// <summary>Deletes an entity by partition key and row key. No-ops if the entity does not exist.</summary>
    protected Task DeleteAsync(string partitionKey, string rowKey, CancellationToken ct = default)
        => _client.DeleteAsync(_tableName, partitionKey, rowKey, ct);

    /// <summary>Queries all entities in the given partition.</summary>
    protected Task<IReadOnlyList<TEntity>> QueryByPartitionAsync(string partitionKey, CancellationToken ct = default)
        => _client.QueryByPartitionAsync<TEntity>(_tableName, partitionKey, ct);

    /// <summary>Queries entities whose row key starts with the given prefix within a partition.</summary>
    protected Task<IReadOnlyList<TEntity>> QueryByRowKeyPrefixAsync(string partitionKey, string prefix, CancellationToken ct = default)
        => _client.QueryByRowKeyPrefixAsync<TEntity>(_tableName, partitionKey, prefix, ct);

    /// <summary>Queries entities whose row key falls within the specified range [start, end) within a partition.</summary>
    protected Task<IReadOnlyList<TEntity>> QueryByRowKeyRangeAsync(string partitionKey, string rowKeyStart, string rowKeyEnd, CancellationToken cancellationToken = default)
        => _client.QueryByRowKeyRangeAsync<TEntity>(_tableName, partitionKey, rowKeyStart, rowKeyEnd, cancellationToken);

}
