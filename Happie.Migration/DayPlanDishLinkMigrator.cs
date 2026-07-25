using System.Text.RegularExpressions;

namespace Happie.Migration;

/// <summary>Contains the core logic for migrating DayPlanDishLink records from old to new format.</summary>
public static partial class DayPlanDishLinkMigrator
{
    // Matches a GUID followed by underscore and an ISO date: {guid}_{yyyy-MM-dd}.
    private static readonly Regex OldFormatPattern = OldFormatRegex();

    /// <summary>Determines whether a PartitionKey is in the old format ({Guid}_{YYYY-MM-DD}).</summary>
    public static bool IsOldFormat(string partitionKey)
    {
        return OldFormatPattern.IsMatch(partitionKey);
    }

    /// <summary>Parses the old-format PartitionKey and RowKey into their component parts.</summary>
    public static OldFormatRecord ParseOldFormat(string partitionKey, string rowKey, int sortOrder)
    {
        var householdId = Guid.Parse(partitionKey[..36]);
        var date = DateOnly.ParseExact(partitionKey[37..], "yyyy-MM-dd");
        var savedDishId = Guid.Parse(rowKey);
        return new OldFormatRecord(householdId, date, savedDishId, sortOrder);
    }

    /// <summary>Builds the new-format PartitionKey for a migrated record.</summary>
    public static string BuildNewPartitionKey(OldFormatRecord record)
    {
        return record.HouseholdId.ToString();
    }

    /// <summary>Builds the new-format RowKey for a migrated record.</summary>
    public static string BuildNewRowKey(OldFormatRecord record)
    {
        return $"{record.Date:yyyy-MM-dd}_{record.SavedDishId}";
    }

    /// <summary>Processes a single record and updates the migration result accordingly.</summary>
    public static async Task ProcessRecordAsync(
        string partitionKey,
        string rowKey,
        int sortOrder,
        Func<string, string, Task<bool>> existsAsync,
        Func<string, string, int, Task> createAsync,
        Func<string, string, Task> deleteAsync,
        MigrationResult result)
    {
        if (!IsOldFormat(partitionKey))
            return;

        var parsed = ParseOldFormat(partitionKey, rowKey, sortOrder);
        var newPartitionKey = BuildNewPartitionKey(parsed);
        var newRowKey = BuildNewRowKey(parsed);

        try
        {
            var alreadyExists = await existsAsync(newPartitionKey, newRowKey);

            if (alreadyExists)
            {
                result.Skipped++;
            }
            else
            {
                await createAsync(newPartitionKey, newRowKey, sortOrder);
                result.Migrated++;
            }

            await deleteAsync(partitionKey, rowKey);
        }
        catch (Exception)
        {
            result.Failed++;
        }
    }

    [GeneratedRegex(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}_\d{4}-\d{2}-\d{2}$", RegexOptions.IgnoreCase)]
    private static partial Regex OldFormatRegex();
}
