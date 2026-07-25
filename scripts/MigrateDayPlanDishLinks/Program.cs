using Azure;
using Azure.Data.Tables;
using System.Text.RegularExpressions;

namespace MigrateDayPlanDishLinks;

public static class Program
{
    private const string TableName = "DayPlanDishLinks";

    // A GUID is 36 characters (8-4-4-4-12 with hyphens). The old format is {Guid}_{YYYY-MM-DD}.
    private static readonly Regex OldFormatPattern = new(
        @"^([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})_(\d{4}-\d{2}-\d{2})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static async Task<int> Main(string[] arguments)
    {
        var connectionString = GetConnectionString(arguments);
        if (connectionString is null)
        {
            Console.WriteLine("Usage: MigrateDayPlanDishLinks <connection-string>");
            Console.WriteLine("  Or set the TABLE_STORAGE_CONNECTION_STRING environment variable.");
            return 1;
        }

        Console.WriteLine($"Starting migration of '{TableName}' table...");
        Console.WriteLine();

        var serviceClient = new TableServiceClient(connectionString);
        var tableClient = serviceClient.GetTableClient(TableName);
        await tableClient.CreateIfNotExistsAsync();

        var migrationResult = await MigrateAsync(tableClient);

        Console.WriteLine();
        Console.WriteLine("=== Migration Complete ===");
        Console.WriteLine($"  Migrated: {migrationResult.Migrated}");
        Console.WriteLine($"  Skipped:  {migrationResult.Skipped}");
        Console.WriteLine($"  Failed:   {migrationResult.Failed}");
        Console.WriteLine($"  Total scanned: {migrationResult.TotalScanned}");

        return migrationResult.Failed > 0 ? 1 : 0;
    }

    public static async Task<MigrationResult> MigrateAsync(TableClient tableClient)
    {
        var migrated = 0;
        var skipped = 0;
        var failed = 0;
        var totalScanned = 0;

        // Scan all entities in the table.
        await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: (string?)null))
        {
            totalScanned++;

            var match = OldFormatPattern.Match(entity.PartitionKey);
            if (!match.Success)
                continue;

            var householdId = match.Groups[1].Value;
            var date = match.Groups[2].Value;
            var savedDishId = entity.RowKey;
            var sortOrder = entity.GetInt32("SortOrder") ?? 0;

            var newPartitionKey = householdId;
            var newRowKey = $"{date}_{savedDishId}";

            try
            {
                // Check if the target record already exists.
                var existingResponse = await GetEntityOrNull(tableClient, newPartitionKey, newRowKey);

                if (existingResponse is not null)
                {
                    // Target already exists; skip creation but still delete the old record.
                    skipped++;
                }
                else
                {
                    // Create the new-format record.
                    var newEntity = new TableEntity(newPartitionKey, newRowKey)
                    {
                        { "SortOrder", sortOrder }
                    };
                    await tableClient.UpsertEntityAsync(newEntity, TableUpdateMode.Replace);
                    migrated++;
                }

                // Delete the old-format record.
                await tableClient.DeleteEntityAsync(entity.PartitionKey, entity.RowKey);
            }
            catch (Exception exception)
            {
                failed++;
                Console.WriteLine($"FAILED: PK={entity.PartitionKey}, RK={entity.RowKey} — {exception.Message}");
            }
        }

        return new MigrationResult(migrated, skipped, failed, totalScanned);
    }

    /// <summary>Checks if a partition key matches the old format: {Guid}_{YYYY-MM-DD}.</summary>
    public static bool IsOldFormat(string partitionKey)
    {
        return OldFormatPattern.IsMatch(partitionKey);
    }

    /// <summary>Parses old-format partition key into household ID and date components.</summary>
    public static (string HouseholdId, string Date)? ParseOldFormat(string partitionKey)
    {
        var match = OldFormatPattern.Match(partitionKey);
        if (!match.Success)
            return null;

        return (match.Groups[1].Value, match.Groups[2].Value);
    }

    private static string? GetConnectionString(string[] arguments)
    {
        if (arguments.Length > 0)
            return arguments[0];

        return Environment.GetEnvironmentVariable("TABLE_STORAGE_CONNECTION_STRING");
    }

    private static async Task<TableEntity?> GetEntityOrNull(TableClient tableClient, string partitionKey, string rowKey)
    {
        try
        {
            var response = await tableClient.GetEntityAsync<TableEntity>(partitionKey, rowKey);
            return response.Value;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }
}

