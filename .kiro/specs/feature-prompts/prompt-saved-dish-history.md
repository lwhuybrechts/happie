# Feature Prompt: Saved Dish History

## Summary

The Saved Dish History feature adds an audit trail for changes made to Saved_Dish records, following the same pattern as the existing `DayHistoryEntry` model used for day plan changes. Each tracked operation (add, rename, soft-delete, reactivate) produces a history entry recording the change type, the acting housemate, and the timestamp. This provides transparency into who modified the household's saved dish collection and when, enabling housemates to review and understand changes over time.

## User Story

As a housemate, I want to see a history of changes made to saved dishes (who added, renamed, deleted, or reactivated them and when), so that I can understand how the household's dish collection has evolved and identify who made specific changes.

## Key Behaviors to Specify

1. **Tracked operations** — The following Saved_Dish operations produce history entries: add (new creation), rename (description update), soft-delete, and reactivate (re-adding a soft-deleted dish). Each operation maps to a `ChangeType` enum value.
2. **History entry structure** — Each entry records: `SavedDishId`, `HouseholdId`, `ChangeType`, `ChangedByHousemateId` (from `X-Housemate-Id` header), `ChangedAt` (UTC timestamp), and optionally a `Parameters` field encoding before/after values (e.g., old and new description on rename).
3. **Storage pattern** — Follow the `DayHistoryEntry` pattern with PartitionKey = `{HouseholdId}` and RowKey = `{SavedDishId}_{InvertedTimestamp}` for reverse-chronological ordering per dish. Alternatively, consider a combined RowKey pattern that supports querying history across all dishes in a household.
4. **Display** — Define where and how history is shown: on the SavedDishesPage (per-dish expand/collapse?), a separate history view, or inline with the dish list. Consider pagination for dishes with many changes.
5. **Translation keys** — History entries use translation keys (like `history_saved_dish_added`, `history_saved_dish_renamed`, etc.) resolved via `SharedStringResolver` for localized display.
6. **Retention** — Define whether history entries are retained indefinitely or pruned after a time period.

## Affected Components and Data Models

| Component | Impact |
|---|---|
| `Happie.Shared/Domain/ChangeType.cs` | Add new enum values: `SavedDishAdded`, `SavedDishRenamed`, `SavedDishDeleted`, `SavedDishReactivated` |
| `SavedDishHandler` | Emit history entries on create, update, delete, reactivate operations |
| New: `SavedDishHistoryEntry` domain record | Similar to `DayHistoryEntry` but scoped to a SavedDish |
| New: `SavedDishHistoryEntity` | Table Storage entity with PK=`{HouseholdId}`, RK=`{SavedDishId}_{InvertedTimestamp}` |
| New: `ISavedDishHistoryRepository` / `SavedDishHistoryRepository` | CRUD for SavedDishHistory table |
| New: `ISavedDishHistoryMapper` / `SavedDishHistoryMapper` | Entity ↔ domain mapping |
| `SavedDishesFunction` | New endpoint: `GET /api/saved-dishes/{id}/history` |
| `SavedDishesPage` | UI for displaying history per dish |
| `SharedStrings.resx` / `SharedStrings.en.resx` | New translation keys for saved dish history messages |
| `Happie.Shared/Contracts/` | New DTO for saved dish history entries |
| `AppStrings.resx` / `AppStrings.en.resx` | Localization keys for history section UI |
