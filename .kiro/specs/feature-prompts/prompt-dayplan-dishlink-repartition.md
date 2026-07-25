# Feature Prompt: DayPlanDishLink Repartitioning

## Summary

Change the `DayPlanDishLinkEntity` partition key strategy from `{HouseholdId}_{YYYY-MM-DD}` to `{HouseholdId}`, with row key `{YYYY-MM-DD}_{SavedDishId}`. This enables efficient single-partition queries for both day plan lookups and statistics/date-range queries, eliminating the need for cross-partition scans.

## Motivation

The current partitioning (`PK={HouseholdId}_{Date}`, `RK={SavedDishId}`) is optimized for a single access pattern: "get all linked dishes for a specific day." However, the upcoming Statistics feature requires querying dish links across date ranges (e.g., "all days SavedDish X was used in the last 3 months"). With the current scheme, this requires a cross-partition scan via `QueryByPartitionPrefixAsync`, which grows linearly with time and becomes increasingly inefficient.

Since multi-dish-selection was only just released, the data volume is minimal — making now the ideal time to change the partition strategy before more records accumulate.

## Proposed Key Scheme

| | Current | Proposed |
|---|---|---|
| **PartitionKey** | `{HouseholdId}_{YYYY-MM-DD}` | `{HouseholdId}` |
| **RowKey** | `{SavedDishId}` | `{YYYY-MM-DD}_{SavedDishId}` |

## Query Performance Comparison

| Query | Current | Proposed |
|---|---|---|
| Get links for a specific date | Single partition query (fast) | Single partition + RowKey prefix `"2025-07-25_"` (equally fast) |
| Get all links for a household | Cross-partition prefix scan (slow, grows with time) | Single partition query (fast, constant) |
| Get links in a date range | Cross-partition scan + client filter (slow) | Single partition + RowKey range filter (fast) |
| Delete all links for a date | Single partition query + delete each (fast) | RowKey prefix query + delete each (equally fast) |

## Backwards Compatibility

Since the data volume is low (feature released days ago), the migration approach should be:

1. **Change the entity key scheme** in `DayPlanDishLinkEntity` to use the new PK/RK pattern
2. **Update the mapper** to encode/decode the date from the RowKey instead of the PartitionKey
3. **Update repository methods** (`GetByDateAsync`, `ReplaceAllAsync`, `DeleteAllAsync`, `GetAllByHouseholdAsync`) to use the new key pattern
4. **Write a one-time data migration** that reads all existing records (old format) and rewrites them with the new key format, then deletes the old records
5. **Update all tests** that construct `DayPlanDishLinkEntity` instances or mock the repository

## Affected Components

| Component | Change |
|---|---|
| `DayPlanDishLinkEntity` | New constructor with `PK={HouseholdId}`, `RK={Date}_{SavedDishId}`; add `Date` property to entity |
| `IDayPlanDishLinkMapper` / `DayPlanDishLinkMapper` | Parse date from RowKey prefix instead of PartitionKey |
| `IDayPlanDishLinkRepository` / `DayPlanDishLinkRepository` | Update all methods to use new key scheme; `GetByDateAsync` uses `QueryByRowKeyPrefixAsync` |
| `BaseRepository` / `ITableStorageClient` | May need a RowKey range query method (e.g., `QueryByRowKeyRangeAsync`) for date-range statistics |
| Integration tests | Update test setup and assertions |
| Property-based tests | Update generators and scenarios |
| One-time migration script | New: migrate old-format records to new format |

## Notes

- The `SortOrder` property remains unchanged — it still represents the order dishes were selected
- `GetAllByHouseholdAsync` becomes a simple `QueryByPartitionAsync` instead of a prefix scan
- A new method `GetByDateRangeAsync(householdId, from, to)` can be added for the Statistics feature using RowKey range filtering
