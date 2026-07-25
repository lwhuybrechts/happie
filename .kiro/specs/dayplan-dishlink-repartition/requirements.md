# Requirements Document

## Introduction

This feature changes the `DayPlanDishLinkEntity` partition key strategy from `{HouseholdId}_{YYYY-MM-DD}` (PartitionKey) / `{SavedDishId}` (RowKey) to `{HouseholdId}` (PartitionKey) / `{YYYY-MM-DD}_{SavedDishId}` (RowKey). The new key scheme groups all dish links for a household into a single partition, enabling efficient single-partition queries for day plan lookups, household-wide queries, and future date-range queries without cross-partition scans. A one-time data migration rewrites existing records from the old format to the new format, and all API contracts remain unchanged to ensure backwards compatibility with all client versions.

## Glossary

- **DayPlanDishLink_Entity**: The Azure Table Storage entity representing a link between a day plan (date) and a saved dish within a household. Stored in the `DayPlanDishLinks` table.
- **DayPlanDishLink_Mapper**: The stateless mapper responsible for converting between `DayPlanDishLinkEntity` (storage representation) and `DayPlanDishLink` (domain record).
- **DayPlanDishLink_Repository**: The repository providing data access methods for day plan dish link records, including query, create, replace, and delete operations.
- **Migration_Script**: A one-time executable script that reads all existing `DayPlanDishLinkEntity` records in the old key format and rewrites them using the new key format.
- **Old_Key_Format**: The partition key scheme `PK={HouseholdId}_{YYYY-MM-DD}`, `RK={SavedDishId}`.
- **New_Key_Format**: The partition key scheme `PK={HouseholdId}`, `RK={YYYY-MM-DD}_{SavedDishId}`.
- **RowKey_Prefix_Query**: A Table Storage query that filters entities within a single partition by a RowKey string prefix, using range comparison operators.
- **SortOrder**: A 0-based integer property on the entity representing the order in which a dish was selected for a day plan.
- **TableStorageClient**: The typed wrapper around Azure Table Storage providing CRUD and query operations (`ITableStorageClient`).

## Requirements

### Requirement 1: Entity Key Scheme Change

**User Story:** As a developer, I want the `DayPlanDishLinkEntity` to use `{HouseholdId}` as PartitionKey and `{YYYY-MM-DD}_{SavedDishId}` as RowKey, so that all dish links for a household reside in a single partition enabling efficient queries.

#### Acceptance Criteria

1. THE DayPlanDishLink_Entity SHALL use the format `{HouseholdId}` as its PartitionKey, where `{HouseholdId}` is the lowercase string representation of the household GUID.
2. THE DayPlanDishLink_Entity SHALL use the format `{YYYY-MM-DD}_{SavedDishId}` as its RowKey, where `{YYYY-MM-DD}` is the ISO 8601 date (e.g. `2025-03-15`) and `{SavedDishId}` is the lowercase string representation of the saved dish GUID, separated by a single underscore character.
3. THE DayPlanDishLink_Entity SHALL retain its `SortOrder` property as a non-negative integer (0-based) representing the selection order of the dish.
4. THE DayPlanDishLink_Entity SHALL include a parameterless constructor for Azure Table Storage deserialization.
5. THE DayPlanDishLink_Entity SHALL include a parameterized constructor accepting `(Guid householdId, DateOnly date, Guid savedDishId)` that assigns PartitionKey and RowKey according to the formats defined in criteria 1 and 2.
6. THE DayPlanDishLink_Entity SHALL support date-scoped queries by allowing a RowKey prefix filter of `{YYYY-MM-DD}_` to retrieve all dish links for a specific date within the household partition.

### Requirement 2: Mapper Update

**User Story:** As a developer, I want the `DayPlanDishLinkMapper` to encode/decode the date from the RowKey instead of the PartitionKey, so that the mapper correctly handles the new key format.

#### Acceptance Criteria

1. WHEN converting from entity to domain model, THE DayPlanDishLink_Mapper SHALL parse the `HouseholdId` from the PartitionKey by interpreting the entire PartitionKey value as a GUID string.
2. WHEN converting from entity to domain model, THE DayPlanDishLink_Mapper SHALL extract the date as the first 10 characters of the RowKey (format `YYYY-MM-DD`) and the `SavedDishId` as the substring after the 11th character (skipping the underscore separator at position 10).
3. WHEN converting from domain model to entity, THE DayPlanDishLink_Mapper SHALL set the PartitionKey to the `HouseholdId` GUID as a lowercase string without braces, set the RowKey to `{YYYY-MM-DD}_{SavedDishId}` where `SavedDishId` is a lowercase GUID string without braces, and set the entity `SortOrder` property to the domain model's `SortOrder` value.
4. WHEN converting from entity to domain model, THE DayPlanDishLink_Mapper SHALL map the entity `SortOrder` property to the domain model's `SortOrder` field.
5. FOR ALL valid DayPlanDishLink domain records (non-empty `HouseholdId`, non-empty `SavedDishId`, and a date within the `DateOnly` range), mapping to entity via `ToEntity` then back to domain model via `ToModel` SHALL produce a record where all four fields (`HouseholdId`, `Date`, `SavedDishId`, `SortOrder`) are equal to the original.

### Requirement 3: Repository Method Updates

**User Story:** As a developer, I want the repository methods to use the new key pattern, so that all data access operations work correctly with the repartitioned data.

#### Acceptance Criteria

1. WHEN `GetByDateAsync` is called with a household ID and date, THE DayPlanDishLink_Repository SHALL call `QueryByRowKeyPrefixAsync` on partition `{HouseholdId}` with RowKey prefix `{YYYY-MM-DD}_`, map the returned entities to domain objects, and return them sorted by SortOrder ascending.
2. WHEN `ReplaceAllAsync` is called with a household ID, date, and list of links, THE DayPlanDishLink_Repository SHALL first delete all existing entities for that date (using the same RowKey prefix query as `DeleteAllAsync`) and then upsert each new link sequentially, preserving the SortOrder values from the provided list.
3. WHEN `DeleteAllAsync` is called with a household ID and date, THE DayPlanDishLink_Repository SHALL query partition `{HouseholdId}` using RowKey prefix `{YYYY-MM-DD}_`, and delete each returned entity by its PartitionKey and RowKey.
4. WHEN `GetAllByHouseholdAsync` is called with a household ID, THE DayPlanDishLink_Repository SHALL call `QueryByPartitionAsync` on partition `{HouseholdId}` and return all entities mapped to domain objects.
5. WHEN `CreateAsync` is called with a DayPlanDishLink, THE DayPlanDishLink_Repository SHALL map the domain object to an entity using PartitionKey `{HouseholdId}` and RowKey `{YYYY-MM-DD}_{SavedDishId}`, and upsert it.
6. IF `ReplaceAllAsync` is called with an empty list of links, THEN THE DayPlanDishLink_Repository SHALL delete all existing entities for that date and not create any new entities.

### Requirement 4: Data Migration

**User Story:** As a developer, I want a one-time migration script that rewrites existing records from the old key format to the new key format, so that all existing data is accessible under the new partition strategy.

#### Acceptance Criteria

1. WHEN executed, THE Migration_Script SHALL scan the DayPlanDishLinks table and identify all records in the Old_Key_Format by detecting PartitionKey values matching the pattern `{HouseholdId}_{YYYY-MM-DD}`.
2. WHEN an old-format record is found, THE Migration_Script SHALL create a corresponding record in the New_Key_Format (PK=`{HouseholdId}`, RK=`{YYYY-MM-DD}_{SavedDishId}`) with identical `SortOrder` value.
3. WHEN the new-format record is successfully written, THE Migration_Script SHALL delete the old-format record.
4. IF a record already exists in the New_Key_Format (matching PK and RK), THEN THE Migration_Script SHALL skip creation for that record and proceed to delete the old-format record.
5. WHEN the migration completes, THE Migration_Script SHALL output the total number of records migrated, the number of records skipped (already in new format), and the number of records that failed.
6. IF a write or delete operation fails for a single record, THEN THE Migration_Script SHALL log the error details to standard output and continue processing remaining records.

### Requirement 5: Backwards Compatibility

**User Story:** As a developer, I want all existing API contracts to remain unchanged after the repartitioning, so that users on any version of the web app continue to receive correct data.

**Assumption:** The application will not be in use during the migration. The migration runs while the app is offline. After migration completes, all data exists exclusively in the New_Key_Format. However, users may still be running older versions of the web app that call the same API endpoints.

#### Acceptance Criteria

1. THE DayPlanDishLink_Repository SHALL return identical domain objects (same `HouseholdId`, `Date`, `SavedDishId`, `SortOrder`) as before the repartitioning.
2. THE DayPlanDishLink_Repository SHALL expose the same public interface methods with the same signatures as before the repartitioning.
3. WHEN any API endpoint that uses dish link data is called, THE response shape and content SHALL remain unchanged from before the repartitioning, regardless of which web app version the client is running.
4. THE IDayPlanDishLinkRepository interface SHALL NOT introduce any breaking changes to existing method signatures.

### Requirement 6: Household Query Optimization

**User Story:** As a developer, I want `GetAllByHouseholdAsync` to use a single-partition query instead of a cross-partition prefix scan, so that household-wide queries are efficient and scalable.

#### Acceptance Criteria

1. WHEN `GetAllByHouseholdAsync` is called, THE DayPlanDishLink_Repository SHALL execute a single-partition query using `QueryByPartitionAsync` on partition key `{HouseholdId}` instead of using `QueryByPartitionPrefixAsync`.
2. THE DayPlanDishLink_Repository SHALL return all dish links for the household from the single-partition query, mapped to domain objects via the mapper.

### Requirement 7: Steering Documentation Update

**User Story:** As a developer, I want the workspace steering documents to reflect the new partition key scheme, so that future development follows the correct conventions.

#### Acceptance Criteria

1. WHEN the repartitioning is implemented, THE `entity-conventions.md` steering file SHALL update the `DayPlanDishLinkEntity` row in the PartitionKey/RowKey patterns table from `PK={HouseholdId}_{YYYY-MM-DD}`, `RK={SavedDishId}` to `PK={HouseholdId}`, `RK={YYYY-MM-DD}_{SavedDishId}`.
2. IF any other steering document references the old DayPlanDishLink key scheme, THEN it SHALL be updated to reflect the new scheme.

### Requirement 8: Date-Range Query Support

**User Story:** As a developer, I want the infrastructure to support RowKey range filtering within a household partition, so that future features can efficiently query dish links across date ranges.

#### Acceptance Criteria

1. THE TableStorageClient SHALL provide a method to query entities within a partition where the RowKey is greater than or equal to a specified `rowKeyStart` string and less than a specified `rowKeyEnd` string.
2. WHEN a RowKey range query is executed, THE TableStorageClient SHALL return all entities in the specified partition whose RowKey is lexicographically >= `rowKeyStart` and < `rowKeyEnd`, ordered by RowKey ascending.
3. WHEN a RowKey range query matches zero entities, THE TableStorageClient SHALL return an empty list.
4. THE BaseRepository SHALL expose a protected method that delegates to the TableStorageClient RowKey range query, following the same pattern as the existing `QueryByRowKeyPrefixAsync` wrapper.
5. IF `rowKeyStart` is lexicographically greater than or equal to `rowKeyEnd`, THEN THE TableStorageClient SHALL return an empty list.
