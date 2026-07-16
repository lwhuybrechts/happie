# Implementation Plan: Multi-Dish Selection

## Overview

This plan transforms the single-dish reference model into a many-to-many relationship between day plans and saved dishes. A new `DayPlanDishLinks` join table replaces the `SavedDishId` field on `DishRecord`, the API contract changes from `savedDishId` to `savedDishIds`, and the frontend `SavedDishModal` converts from single-select to multi-select with a sticky footer, live preview, and confirm button. The implementation proceeds backend-first (data model → repository → handler → function), then frontend (contracts → cached client → components), then cross-cutting concerns (modal fixes, scroll lock, offline support, steering docs, prompt doc).

## Tasks

- [x] 1. Backend data model and infrastructure
  - [x] 1.1 Create `DayPlanDishLink` domain record
    - Create `Happie.Api/Domain/DayPlanDishLink.cs` with properties: `HouseholdId` (Guid), `Date` (DateOnly), `SavedDishId` (Guid), `SortOrder` (int)
    - _Requirements: 1.1, 1.2, 1.3_
  - [x] 1.2 Create `DayPlanDishLinkEntity` table storage entity
    - Create `Happie.Api/Infrastructure/Entities/DayPlanDishLinkEntity.cs` inheriting `MyTableEntity`
    - PK = `{HouseholdId}_{YYYY-MM-DD}`, RK = `{SavedDishId}`
    - Include `SortOrder` (int) property
    - Follow entity conventions: parameterless constructor, parameterized constructor setting PK/RK
    - _Requirements: 1.1, 1.2, 1.3_
  - [x] 1.3 Create `IDayPlanDishLinkMapper` and `DayPlanDishLinkMapper`
    - Create interface in `Happie.Api/Infrastructure/Mappers/IDayPlanDishLinkMapper.cs`
    - Create implementation in `Happie.Api/Infrastructure/Mappers/DayPlanDishLinkMapper.cs`
    - `ToModel` parses composite PK (`{HouseholdId}_{YYYY-MM-DD}`) and RK (`{SavedDishId}`)
    - `ToEntity` constructs entity with composite PK and GUID RK
    - _Requirements: 1.1, 1.2, 1.3_
  - [x]* 1.4 Write property test for DayPlanDishLink mapper round-trip
    - **Property 1: DayPlanDishLink mapper round-trip**
    - **Validates: Requirements 1.1, 1.3**
  - [x] 1.5 Create `IDayPlanDishLinkRepository` and `DayPlanDishLinkRepository`
    - Create interface in `Happie.Api/Infrastructure/Repositories/IDayPlanDishLinkRepository.cs` with methods: `GetByDateAsync`, `ReplaceAllAsync`, `DeleteAllAsync`, `GetAllByHouseholdAsync`, `CreateAsync`
    - Create implementation in `Happie.Api/Infrastructure/Repositories/DayPlanDishLinkRepository.cs` extending `BaseRepository<DayPlanDishLinkEntity>`
    - Table name: `DayPlanDishLinks`
    - Add `QueryByPartitionPrefixAsync` to `ITableStorageClient` and `BaseRepository` for household-wide queries
    - _Requirements: 1.2, 9.1_
  - [x] 1.6 Remove `SavedDishId` from `DishRecord`, `DishRecordEntity`, and `DishRecordMapper`
    - Remove `SavedDishId` property from `DishRecordEntity.cs`
    - Remove `SavedDishId` parameter from `DishRecord` record
    - Update `DishRecordMapper` to no longer map `SavedDishId`
    - Update all existing usages in `DayHandler`, `SavedDishHandler`, `DaysFunction`, and tests
    - _Requirements: 1.4_
  - [x] 1.7 Register new mapper and repository in `Program.cs`
    - Add singleton registration for `IDayPlanDishLinkMapper` / `DayPlanDishLinkMapper`
    - Add singleton registration for `IDayPlanDishLinkRepository` / `DayPlanDishLinkRepository`
    - _Requirements: 1.2_

- [x] 2. Shared contracts update
  - [x] 2.1 Update `DishDto` to replace `savedDishId` with `savedDishIds`
    - Replace nullable `Guid? SavedDishId` with nullable `IReadOnlyList<Guid>? SavedDishIds` in `Happie.Shared/Contracts/DishDto.cs`
    - Add `[JsonPropertyName("savedDishIds")]` attribute
    - Update any frontend code that reads `SavedDishId` to use `SavedDishIds`
    - _Requirements: 10.2_
  - [x] 2.2 Update `UpdateDishRequest` to replace `savedDishId` with `savedDishIds`
    - Replace nullable `Guid? SavedDishId` with nullable `IReadOnlyList<Guid>? SavedDishIds` in `Happie.Shared/Contracts/UpdateDishRequest.cs`
    - Add `[JsonPropertyName("savedDishIds")]` attribute
    - Update `DaysFunction` to pass the new field to the handler
    - _Requirements: 10.1_

- [ ] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. DayHandler rewrite for multi-dish support
  - [x] 4.1 Update `IDayHandler.UpsertDishAsync` signature
    - Change parameter from `Guid? savedDishId` to `IReadOnlyList<Guid>? savedDishIds`
    - Update `DayHandler` constructor to inject `IDayPlanDishLinkRepository`
    - _Requirements: 10.1, 10.3, 10.4_
  - [x] 4.2 Rewrite `DayHandler.UpsertDishAsync` for multi-dish saves
    - Implement mutual exclusion validation (both `savedDishIds` non-empty AND description non-empty → 422)
    - Implement empty save (both null/empty → delete links + handle DishRecord based on DinnerTime)
    - Implement saved-mode save (validate ≤10 IDs, no duplicates, all exist in household, replace links, clear description)
    - Implement Auto_Match logic for custom-mode saves (case-insensitive trimmed match, reactivate soft-deleted, create link)
    - Implement custom-mode save without match (delete existing links, store description)
    - Add `DishUpsertResult.SavedDishNotFound` if not already present
    - _Requirements: 3.4, 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 10.3, 10.4, 10.5, 16.1, 16.2, 16.3, 16.4_
  - [x] 4.3 Rewrite `DayHandler.GetDayPlanAsync` dish resolution
    - Fetch `DayPlanDishLink` entities for the date
    - When links exist: resolve descriptions from saved dishes, join with " & " in SortOrder, exclude missing SavedDishIds, return valid IDs in `savedDishIds`
    - When links exist: ignore DishRecord description
    - When no links: use DishRecord description (existing behavior), return null `savedDishIds`
    - Resolve descriptions from soft-deleted saved dishes (still visible)
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7_
  - [x] 4.4 Update `DayHandler.DeleteDishAsync` to also delete links
    - When deleting a dish, also call `_dayPlanDishLinkRepository.DeleteAllAsync` for that household+date
    - _Requirements: 5.7_
  - [ ]* 4.5 Write property test for combined description resolution
    - **Property 2: Combined description resolution**
    - **Validates: Requirements 1.5, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6**
  - [ ]* 4.6 Write property test for save creates and replaces links correctly
    - **Property 3: Save creates and replaces links correctly**
    - **Validates: Requirements 5.2, 5.3, 5.5, 5.7**
  - [ ]* 4.7 Write property test for input validation
    - **Property 4: Input validation rejects invalid dish save requests**
    - **Validates: Requirements 3.4, 5.4, 5.6, 10.5**
  - [ ]* 4.8 Write property test for Auto_Match
    - **Property 5: Auto_Match links matching saved dish and reactivates if soft-deleted**
    - **Validates: Requirements 16.1, 16.2, 16.3, 16.4**

- [x] 5. Update DaysFunction and SavedDishHandler
  - [x] 5.1 Update `DaysFunction` to pass `savedDishIds` to handler
    - Change the dish PUT endpoint to extract `savedDishIds` from `UpdateDishRequest` and pass to `UpsertDishAsync`
    - _Requirements: 10.1, 10.3_
  - [x] 5.2 Rewrite `SavedDishHandler` retroactive conversion to use join table
    - Replace `ConvertMatchingDishRecordsAsync` to create `DayPlanDishLink` entities instead of setting `SavedDishId`
    - Inject `IDayPlanDishLinkRepository` into `SavedDishHandler`
    - Only convert DishRecords that have no existing links (check via `GetAllByHouseholdAsync`)
    - Clear DishRecord description after creating link
    - Log failures but don't roll back
    - _Requirements: 9.1, 9.2, 9.3_
  - [x] 5.3 Update `SavedDishHandler.GetSuggestionsAsync` for new model
    - Replace `SavedDishId is null` check with "no DayPlanDishLink entities exist for this DishRecord"
    - Query links by household to determine which dates already have links
    - _Requirements: 1.4, 1.5_
  - [ ]* 5.4 Write property test for retroactive conversion
    - **Property 6: Retroactive conversion creates links for all matching DishRecords**
    - **Validates: Requirements 9.1, 9.2**

- [ ] 6. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Frontend multi-select modal
  - [x] 7.1 Update `SavedDishModal` to multi-select with checkmarks
    - Convert from single-select (tap to close) to multi-select (toggleable checkmarks)
    - Display all active saved dishes sorted alphabetically
    - Track selection state as an ordered list of selected SavedDishIds
    - Pre-select currently linked dishes when opening in Saved_Mode
    - Auto-match and pre-select when custom description matches a saved dish (scroll to match)
    - Enforce Selection_Limit of 10 (disable unselected checkmarks at limit, re-enable on deselect)
    - Dismiss (close/backdrop) preserves previous state without changes
    - All text uses `IStringLocalizer<AppStrings>`
    - _Requirements: 2.1, 2.2, 2.5, 2.6, 2.7, 2.8, 2.10, 3.1, 3.2, 3.3_
  - [x] 7.2 Implement sticky footer with live preview and confirm button
    - Add fixed footer showing: Combined_Description preview (joined with " & " in selection order), "Confirm (N)" button (disabled when N=0), "Custom mode" button
    - Immediate update on toggle (count + preview text)
    - Show localized placeholder when no dishes selected
    - Footer remains visible at all times regardless of scroll position
    - "Confirm" button text includes count via `IStringLocalizer<AppStrings>`
    - _Requirements: 2.3, 2.4, 2.9, 12.1, 12.2, 12.3, 12.4, 12.5_
  - [x] 7.3 Implement Promote_Option in Multi_Select_Modal
    - Show Promote_Option at top of list when custom description is non-empty, non-matching, ≤100 chars
    - On select: create new SavedDish via `POST /api/saved-dishes`, on success auto-check new dish
    - Handle 409 (already exists): show localized error, auto-check existing match
    - Handle network/server error: show localized error, remain open
    - Hide when description is empty, whitespace-only, or >100 chars
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

- [ ] 8. Frontend DishPanel and mode toggle updates
  - [ ] 8.1 Update DishPanel display for multi-dish selection
    - Display Combined_Description (joined with " & ") in read mode when saved dishes are linked
    - Show bookmark icon adjacent to Combined_Description when linked dishes exist
    - When entering edit mode with linked dishes: start in Saved_Mode with dishes pre-selected
    - When switching from Saved_Mode to Custom_Mode: pre-fill input with Combined_Description
    - _Requirements: 6.1, 6.2, 6.3, 6.4_
  - [ ] 8.2 Update Mode_Toggle behavior
    - Bookmark button always opens Multi_Select_Modal (regardless of current mode)
    - From Saved_Mode: open modal with currently linked dishes pre-selected
    - From Custom_Mode: open modal (existing behavior)
    - "Custom mode" button in footer: close modal, switch to Custom_Mode, pre-fill with Combined_Description (or existing custom description if no dishes selected)
    - _Requirements: 17.1, 17.2, 17.3, 17.4_
  - [ ] 8.3 Update DishPanel save logic for multi-dish
    - On confirm: send list of selected SavedDishIds (in selection order) with null description
    - On custom save: send null SavedDishIds with typed description
    - Wire through `CachedApiClient.SaveDishAsync` with updated signature
    - _Requirements: 5.1, 5.5_

- [ ] 9. Offline support and CachedApiClient updates
  - [ ] 9.1 Extend `CachedApiClient.SaveDishAsync` for multi-dish
    - Update signature to accept `IReadOnlyList<Guid>? savedDishIds` and `string? resolvedDescription`
    - When `savedDishIds` is non-empty: send with null description
    - Optimistic update: store `resolvedDescription` in cached `DishDto.Description` and `savedDishIds` in `DishDto.SavedDishIds`
    - Offline: enqueue mutation with savedDishIds; cache is updated immediately
    - _Requirements: 8.1, 8.2, 8.3, 8.4_
  - [ ]* 9.2 Write unit tests for CachedApiClient multi-dish offline behavior
    - Test optimistic update stores Combined_Description and savedDishIds in cache
    - Test offline queueing includes savedDishIds in mutation payload
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

- [ ] 10. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 11. Modal overlay and scroll lock fixes
  - [ ] 11.1 Fix modal overlay positioning
    - Update Multi_Select_Modal overlay to use `position: fixed`, `inset: 0`, `z-index: 1100`
    - Update modal dialog to use `z-index: 1101`
    - Ensure overlay covers entire viewport including day plan header
    - Match NudgeModal overlay behavior
    - _Requirements: 13.1, 13.2, 13.3_
  - [ ] 11.2 Implement Scroll_Lock for all modals
    - Apply `overflow: hidden` to document body when any modal is open (Multi_Select_Modal, NudgeModal, HousemateColorPicker)
    - Restore previous scroll behavior on close (confirm, dismiss, backdrop)
    - Apply universally to all existing and new modals
    - _Requirements: 14.1, 14.2, 14.3_
  - [ ] 11.3 Implement scrollable modal content
    - Make modal body independently scrollable when saved dishes list exceeds available vertical space
    - Keep modal header and sticky footer fixed (not scrolling with list)
    - Contain scroll within modal (page does not scroll)
    - _Requirements: 15.1, 15.2, 15.3_

- [ ] 12. Documentation and prompt document
  - [ ] 12.1 Update `coding-conventions.md` with modal conventions
    - Document modal overlay `position: fixed`, `inset: 0`, `z-index: 1100` convention
    - Document Scroll_Lock (`overflow: hidden` on body) requirement for all modals
    - Document independently scrollable modal content with fixed header/footer
    - _Requirements: 18.1, 18.2, 18.3, 18.4_
  - [ ] 12.2 Create `prompt-dish-folders.md` in spec directory
    - Write one-paragraph feature summary for dish folders/categories
    - Include user story
    - List key behaviors to be specified (folder CRUD, assigning dishes, filtering, modal integration)
    - List affected components and data models (SavedDish, DayPlanDishLink, Multi_Select_Modal)
    - Describe relationship to existing entities
    - _Requirements: 11.1, 11.2, 11.3, 11.4_

- [ ] 13. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The `SavedDishId` removal (task 1.6) will cause compile errors that are resolved by tasks 2.1, 2.2, 4.1–4.4, 5.1–5.3; these should be done in sequence
- `QueryByPartitionPrefixAsync` (task 1.5) requires extending `ITableStorageClient` — check if a filter-based query method already exists

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["1.4", "1.5"] },
    { "id": 2, "tasks": ["1.6", "1.7"] },
    { "id": 3, "tasks": ["2.1", "2.2"] },
    { "id": 4, "tasks": ["4.1"] },
    { "id": 5, "tasks": ["4.2", "4.3", "4.4"] },
    { "id": 6, "tasks": ["4.5", "4.6", "4.7", "4.8", "5.1", "5.2", "5.3"] },
    { "id": 7, "tasks": ["5.4"] },
    { "id": 8, "tasks": ["7.1", "8.1", "9.1"] },
    { "id": 9, "tasks": ["7.2", "8.2"] },
    { "id": 10, "tasks": ["7.3", "8.3", "9.2"] },
    { "id": 11, "tasks": ["11.1", "11.2", "11.3"] },
    { "id": 12, "tasks": ["12.1", "12.2"] }
  ]
}
```
