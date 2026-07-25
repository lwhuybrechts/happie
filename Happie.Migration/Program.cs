using Azure.Data.Tables;
using Happie.Migration;

var connectionString = args.Length > 0
    ? args[0]
    : Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING") ?? "UseDevelopmentStorage=true";

var tableClient = new TableClient(connectionString, "DayPlanDishLinks");
await tableClient.CreateIfNotExistsAsync();

Console.WriteLine("Starting DayPlanDishLink migration...");

var result = new MigrationResult();
var entities = tableClient.QueryAsync<TableEntity>();

await foreach (var entity in entities)
{
    await DayPlanDishLinkMigrator.ProcessRecordAsync(
        entity.PartitionKey,
        entity.RowKey,
        entity.GetInt32("SortOrder") ?? 0,
        async (partitionKey, rowKey) =>
        {
            var existing = await tableClient.GetEntityIfExistsAsync<TableEntity>(partitionKey, rowKey);
            return existing.HasValue;
        },
        async (partitionKey, rowKey, sortOrder) =>
        {
            var newEntity = new TableEntity(partitionKey, rowKey)
            {
                { "SortOrder", sortOrder }
            };
            await tableClient.UpsertEntityAsync(newEntity);
        },
        async (partitionKey, rowKey) =>
        {
            await tableClient.DeleteEntityAsync(partitionKey, rowKey);
        },
        result);
}

Console.WriteLine($"Migration complete. Migrated: {result.Migrated}, Skipped: {result.Skipped}, Failed: {result.Failed}");
