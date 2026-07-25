# Implementation Plan: DayPlanDishLink Repartition

## Overview

Repartition `DayPlanDishLinkEntity` from composite partition key (`{HouseholdId}_{YYYY-MM-DD}`) to single-value partition key (`{HouseholdId}`), moving date into the RowKey prefix. This involves modifying the entity, mapper, repository, extending the infrastructure layer with RowKey range query support, creating a migration script, and updating steering documentation.

## Tasks

- [x] 1. Extend infrastructure with RowKey range query support
  - [x] 1.1 Add `QueryByRowKeyRangeAsync` to `ITableStorageClient`
    - Add method signature `Task<IReadOnlyList<T>> QueryByRowKeyRangeAsync<T>(string tableName, string partitionKey, string rowKeyStart, string rowKeyEnd, CancellationToken cancellationToken = default) where T : MyTableEntity`
    - Return all entities where RowKey >= `rowKeyStart` and < `rowKeyEnd` within the partition
    - Return empty list when `rowKeyStart` >= `rowKeyEnd`
    - _Requirements: 8.1, 8.2, 8.3, 8.5_

  - [x] 1.2 Implement `QueryByRowKeyRangeAsync` in `TableStorageClient`
    - Use `TableClient.CreateQueryFilter` with PartitionKey, RowKey ge/lt conditions
    - Follow the same pattern as `QueryByRowKeyPrefixAsync` (CreateIfNotExists, build filter, iterate results)
    - Return empty list when `rowKeyStart` >= `rowKeyEnd` (guard clause)
    - _Requirements: 8.1, 8.2, 8.3, 8.5_

  - [x] 1.3 Add `QueryByRowKeyRangeAsync` wrapper to `BaseRepository`
    - Add protected method delegating to `_client.QueryByRowKeyRangeAsync<TEntity>(_tableName, ...)`
    - Follow same pattern as existing `QueryByRowKeyPrefixAsync` wrapper
    - _Requirements: 8.4_

- [x] 2. Update entity and mapper for new key scheme
  - [x] 2.1 Modify `DayPlanDishLinkEntity` constructor to use new key format
    - Change parameterized constructor to set `PartitionKey = householdId.ToString()` and `RowKey = $"{date:yyyy-MM-dd}_{savedDishId}"`
    - Keep parameterless constructor unchanged
    - Keep `SortOrder` property unchanged
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

  - [x] 2.2 Update `DayPlanDishLinkMapper` for new key encoding
    - `ToModel`: parse `HouseholdId` from entire PartitionKey (no split), extract date from RowKey[..10], extract SavedDishId from RowKey[11..]
    - `ToEntity`: set PartitionKey to HouseholdId string, RowKey to `{date:yyyy-MM-dd}_{savedDishId}`, SortOrder from domain model
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 2.3 Write property test for mapper round-trip (Property 1)
    - **Property 1: Mapper round-trip preserves all fields**
    - Generate random `Guid` for HouseholdId and SavedDishId, random `DateOnly` (2020–2030), random non-negative `int` for SortOrder (0–999)
    - Assert `ToEntity` → `ToModel` produces equal HouseholdId, Date, SavedDishId, SortOrder
    - Use FsCheck with `[Property(MaxTest = 100)]`
    - Tag: `// Feature: dayplan-dishlink-repartition, Property 1: Mapper round-trip preserves all fields`
    - **Validates: Requirements 1.1, 1.2, 1.3, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 5.1**

  - [x] 2.4 Write unit tests for `DayPlanDishLinkEntity` and `DayPlanDishLinkMapper`
    - Test entity constructor sets PK and RK in correct format with known GUID/date values
    - Test mapper with edge dates (leap year 2024-02-29, year boundary 2025-01-01, end of year 2025-12-31)
    - Test parameterless constructor exists and produces valid instance
    - _Requirements: 1.1, 1.2, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4_

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Update repository to use new query patterns
  - [x] 4.1 Modify `DayPlanDishLinkRepository` methods for new partition scheme
    - `GetByDateAsync`: use `QueryByRowKeyPrefixAsync(householdId.ToString(), $"{date:yyyy-MM-dd}_")`, map and sort by SortOrder
    - `DeleteAllAsync`: use same RowKey prefix query, delete each entity by PK/RK
    - `ReplaceAllAsync`: call `DeleteAllAsync` then upsert each new link
    - `GetAllByHouseholdAsync`: use `QueryByPartitionAsync(householdId.ToString())` instead of `QueryByPartitionPrefixAsync`
    - `CreateAsync`: unchanged (entity constructor now handles correct key format)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 6.1, 6.2_

  - [x] 4.2 Write integration tests for `DayPlanDishLinkRepository`
    - Test `GetByDateAsync` returns sorted results for a specific date
    - Test `ReplaceAllAsync` deletes existing and inserts new links
    - Test `ReplaceAllAsync` with empty list deletes all for date
    - Test `DeleteAllAsync` removes all entities for a date
    - Test `GetAllByHouseholdAsync` returns all links from single partition
    - Test `CreateAsync` upserts with correct key format
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 6.1, 6.2_

- [x] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Implement RowKey range query tests
  - [x] 6.1 Write property test for RowKey range query (Property 2)
    - **Property 2: RowKey range query returns exactly the entities within range**
    - Generate random partition key, 0–20 entities with random RowKey strings, random rowKeyStart/rowKeyEnd
    - Insert entities into Azurite, execute `QueryByRowKeyRangeAsync`, verify result matches expected filter (RowKey >= start and < end)
    - Include cases where `rowKeyStart` >= `rowKeyEnd` to verify empty-list behavior
    - Use FsCheck with `[Property(MaxTest = 100)]`
    - Tag: `// Feature: dayplan-dishlink-repartition, Property 2: RowKey range query returns exactly the entities within range`
    - **Validates: Requirements 8.1, 8.2, 8.3, 8.5**

  - [x] 6.2 Write integration tests for `TableStorageClient.QueryByRowKeyRangeAsync`
    - Test correct filtering with known entities and range boundaries
    - Test empty result when no entities match
    - Test empty result when `rowKeyStart` >= `rowKeyEnd`
    - Test ordering is by RowKey ascending
    - _Requirements: 8.1, 8.2, 8.3, 8.5_

- [x] 7. Create migration script
  - [x] 7.1 Implement one-time migration console script
    - Create a standalone console project or C# script in the `scripts/` directory
    - Scan `DayPlanDishLinks` table for all entities
    - Identify old-format records by detecting PartitionKey values matching `{Guid}_{YYYY-MM-DD}` pattern (GUID contains no underscores after position 36)
    - For each old-format record: create new-format record (PK=HouseholdId, RK=date_savedDishId) with same SortOrder, then delete old record
    - Skip creation if target record already exists (idempotent)
    - Log individual failures and continue processing
    - Report totals: migrated, skipped, failed
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_

  - [x] 7.2 Write unit tests for migration script logic
    - Test old-format detection regex/logic with valid and invalid PartitionKey patterns
    - Test skip logic when new-format record already exists
    - Test error accumulation and final totals reporting
    - _Requirements: 4.1, 4.4, 4.5, 4.6_

- [x] 8. Update steering documentation
  - [x] 8.1 Update `entity-conventions.md` with new key scheme
    - Change the `DayPlanDishLinkEntity` row in the PartitionKey/RowKey patterns table from `PK={HouseholdId}_{YYYY-MM-DD}`, `RK={SavedDishId}` to `PK={HouseholdId}`, `RK={YYYY-MM-DD}_{SavedDishId}`
    - Check for any other references to the old key scheme in steering documents and update them
    - _Requirements: 7.1, 7.2_

- [x] 9. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Local validation — Run migration against Azurite and verify
  - [x] 10.1 Run migration script against local Azurite database
    - Execute the migration script targeting the local Azurite Table Storage connection string
    - Verify output reports expected totals (migrated, skipped, failed)
    - Confirm old-format records are deleted and new-format records exist in Azurite
  - [x] 10.2 Manually test the application against the migrated local database
    - Start the Functions app locally against Azurite
    - Exercise day plan dish link operations (get by date, create, replace, delete, get all by household)
    - Confirm all operations work correctly with the migrated data
    - Ask the user to verify via the web app if needed

- [x] 11. Deploy and run production migration
  - [x] 11.1 Deploy updated code to Azure
    - Push changes and trigger deployment (or deploy manually)
    - Confirm the Functions app is running the new code in Azure
  - [x] 11.2 Run migration script against Azure Table Storage
    - Execute the migration script targeting the production Azure Table Storage connection string
    - Verify output reports expected totals (migrated, skipped, failed)
    - Confirm no failures occurred; if failures exist, investigate and re-run (script is idempotent)
  - [x] 11.3 Verify production application works after migration
    - Confirm the live app serves correct data with the new partition scheme
    - Ask the user to do a quick smoke test on the deployed app

- [x] 12. Remove migration script
  - [x] 12.1 Delete migration script project and related test files
    - Remove the migration console project/script directory created in task 7.1
    - Remove any associated unit test files created in task 7.2
    - Remove any project references to the migration project from the solution (if added)
    - Confirm the solution still builds cleanly after removal

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The migration script is designed to be idempotent — safe to re-run
- The `IDayPlanDishLinkRepository` interface remains unchanged (backwards compatibility)
- All API contracts remain unchanged — no handler or function layer changes needed

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1"] },
    { "id": 1, "tasks": ["1.2", "2.2"] },
    { "id": 2, "tasks": ["1.3", "2.3", "2.4"] },
    { "id": 3, "tasks": ["4.1"] },
    { "id": 4, "tasks": ["4.2", "6.1", "6.2"] },
    { "id": 5, "tasks": ["7.1", "7.2", "8.1"] },
    { "id": 6, "tasks": ["10.1"] },
    { "id": 7, "tasks": ["10.2"] },
    { "id": 8, "tasks": ["11.1"] },
    { "id": 9, "tasks": ["11.2"] },
    { "id": 10, "tasks": ["11.3"] },
    { "id": 11, "tasks": ["12.1"] }
  ]
}
```
