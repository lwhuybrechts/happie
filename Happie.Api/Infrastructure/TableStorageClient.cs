using Azure;
using Azure.Data.Tables;

namespace Happie.Api.Infrastructure;

/// <summary>Typed wrapper around Azure Table Storage for common CRUD and query operations.</summary>
public class TableStorageClient : ITableStorageClient
{
    private readonly TableServiceClient _serviceClient;

    /// <summary>Initializes a new instance of <see cref="TableStorageClient"/>.</summary>
    public TableStorageClient(TableServiceClient serviceClient)
    {
        _serviceClient = serviceClient;
    }

    /// <inheritdoc/>
    public async Task UpsertAsync<T>(string tableName, T entity, CancellationToken cancellationToken = default)
        where T : MyTableEntity
    {
        var tableClient = _serviceClient.GetTableClient(tableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken);
        await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<T?> GetAsync<T>(string tableName, string partitionKey, string rowKey, CancellationToken cancellationToken = default)
        where T : MyTableEntity
    {
        var tableClient = _serviceClient.GetTableClient(tableName);
        try
        {
            var response = await tableClient.GetEntityAsync<T>(partitionKey, rowKey, cancellationToken: cancellationToken);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string tableName, string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        var tableClient = _serviceClient.GetTableClient(tableName);
        try
        {
            await tableClient.DeleteEntityAsync(partitionKey, rowKey, cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Entity does not exist; nothing to delete.
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<T>> QueryByPartitionAsync<T>(string tableName, string partitionKey, CancellationToken cancellationToken = default)
        where T : MyTableEntity
    {
        var tableClient = _serviceClient.GetTableClient(tableName);
        var filter = TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey}");
        var results = new List<T>();
        await foreach (var entity in tableClient.QueryAsync<T>(filter, cancellationToken: cancellationToken))
        {
            results.Add(entity);
        }
        return results;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<T>> QueryByRowKeyPrefixAsync<T>(string tableName, string partitionKey, string rowKeyPrefix, CancellationToken cancellationToken = default)
        where T : MyTableEntity
    {
        var tableClient = _serviceClient.GetTableClient(tableName);

        // Row key prefix range: [prefix, prefix + '\uffff') covers all keys starting with the prefix.
        var prefixEnd = rowKeyPrefix + "\uffff";
        var filter = TableClient.CreateQueryFilter(
            $"PartitionKey eq {partitionKey} and RowKey ge {rowKeyPrefix} and RowKey lt {prefixEnd}");

        var results = new List<T>();
        await foreach (var entity in tableClient.QueryAsync<T>(filter, cancellationToken: cancellationToken))
        {
            results.Add(entity);
        }
        return results;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<T>> QueryByPartitionPrefixAsync<T>(string tableName, string partitionKeyPrefix, CancellationToken cancellationToken = default)
        where T : MyTableEntity
    {
        var tableClient = _serviceClient.GetTableClient(tableName);
        var prefixEnd = partitionKeyPrefix + "\uffff";
        var filter = TableClient.CreateQueryFilter(
            $"PartitionKey ge {partitionKeyPrefix} and PartitionKey lt {prefixEnd}");
        var results = new List<T>();
        await foreach (var entity in tableClient.QueryAsync<T>(filter, cancellationToken: cancellationToken))
        {
            results.Add(entity);
        }
        return results;
    }
}
