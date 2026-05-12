using Azure.Data.Tables;

namespace Happie.Api.IntegrationTests.Infrastructure;

/// <summary>Helper for truncating Azure Table Storage tables before tests.</summary>
public static class TableHelper
{
    /// <summary>Deletes all entities from the specified table, creating it first if it does not exist.</summary>
    public static void TruncateTable(TableServiceClient serviceClient, string tableName)
    {
        var tableClient = serviceClient.GetTableClient(tableName);
        tableClient.CreateIfNotExists();

        var entities = tableClient.Query<TableEntity>(select: ["PartitionKey", "RowKey"]).ToList();
        foreach (var entity in entities)
        {
            tableClient.DeleteEntity(entity.PartitionKey, entity.RowKey);
        }
    }
}
