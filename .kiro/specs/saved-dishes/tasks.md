# Implementation Plan: Saved Dishes

## Overview

This plan implements a household-level saved dishes collection with CRUD management, reference-based day plan integration, retroactive conversion, soft-delete, suggestions, and offline support. The implementation spans backend (new Azure Table Storage entity, new API endpoints, DayHandler modifications) and frontend (SavedDishesPage, SavedDishModal, DishPanel mode toggle, CachedApiClient extension). Tasks are ordered so backend data models and infrastructure are built first, then the API layer, then frontend components, and finally integration wiring.

## Tasks

- [x] 1. Backend data model and infrastructure
  - [x] 1.1 Create `SavedDish` domain record and result types
    - Create `Happie.Api/Domain/SavedDish.cs` with `Id`, `HouseholdId`, `Description`, `IsDeleted`
    - Create `Happie.Api/Results/SavedDishCreateResult.cs`, `SavedDishCreateOutcome.cs`, `SavedDishUpdateResult.cs`, `SavedDishUpdateOutcome.cs`, `SavedDishDeleteResult.cs`
    - _Requirements: 1.1_

  - [x] 1.2 Create `SavedDishEntity` and mapper
    - Create `Happie.Api/Infrastructure/Entities/SavedDishEntity.cs` inheriting `MyTableEntity` with PK=`{HouseholdId}`, RK=`{SavedDishId}`
    - Create `Happie.Api/Infrastructure/Mappers/ISavedDishMapper.cs` and `SavedDishMapper.cs`
    - _Requirements: 1.1, 1.2_

  - [x] 1.3 Create `ISavedDishRepository` / `SavedDishRepository`
    - Create repository in `Happie.Api/Infrastructure/Repositories/` for table `SavedDishes`
    - Implement `GetAllAsync`, `GetAsync`, `UpsertAsync` methods returning domain types
    - _Requirements: 1.1, 1.2_

  - [x] 1.4 Extend `DishRecord` domain type with `SavedDishId`
    - Add nullable `Guid? SavedDishId` to the `DishRecord` record
    - Update `DishRecordEntity` with `Guid SavedDishId` property (default `Guid.Empty` as sentinel)
    - Update `DishRecordMapper` to map `Guid.Empty` ↔ `null`
    - _Requirements: 2.1_

  - [x] 1.5 Extend `IDishRepository` / `DishRepository` with `GetAllByPartitionAsync`
    - Add method to query all DishRecords for a household (needed for retroactive conversion and suggestions)
    - _Requirements: 7.1, 5.2_

  - [x] 1.6 Write property test for SavedDish mapper round-trip
    - **Property 1: SavedDish entity mapper round-trip**
    - **Validates: Requirements 1.1, 1.2**

- [x] 2. Implement `SavedDishHandler` business logic
  - [x] 2.1 Create `ISavedDishHandler` / `SavedDishHandler`
    - Implement `GetAllActiveAsync`: filter `IsDeleted == false`, sort alphabetically (case-insensitive)
    - Implement `CreateAsync`: validate description (1–100 chars, trimmed, not empty), check uniqueness (case-insensitive across active + soft-deleted), reactivate soft-deleted matches, create new if no match, trigger retroactive conversion
    - Implement `UpdateAsync`: validate description, check uniqueness excluding self, reject if target not found or soft-deleted
    - Implement `DeleteAsync`: set `IsDeleted = true`, return NotFound if already deleted or missing
    - Implement `GetSuggestionsAsync`: query all DishRecords without SavedDishId, exclude matches against all SavedDishes, return top 5 distinct descriptions ordered by most recent date
    - Register mapper, repositories, and handler as singletons in `Program.cs`
    - _Requirements: 1.3, 1.4, 1.5, 1.6, 3.2, 5.2, 5.3, 6.1, 6.5, 7.1, 7.2, 7.5, 11.1, 11.2, 11.4, 11.6_

  - [x] 2.2 Write property test for create uniqueness and reactivation
    - **Property 3: Create enforces uniqueness and reactivates soft-deleted**
    - **Validates: Requirements 1.3, 1.4, 1.5, 6.5**

  - [x] 2.3 Write property test for active list filtering and sorting
    - **Property 4: Active list excludes soft-deleted and is sorted alphabetically**
    - **Validates: Requirements 3.2, 6.2, 6.3**

  - [x] 2.4 Write property test for suggestions computation
    - **Property 5: Suggestions are distinct, unmatched, recent, and limited to 5**
    - **Validates: Requirements 5.2, 5.3**

  - [x] 2.5 Write property test for retroactive conversion
    - **Property 6: Retroactive conversion links all matching DishRecords**
    - **Validates: Requirements 7.1, 7.2**

  - [x] 2.6 Write property test for update rejection
    - **Property 7: Update rejected when description unchanged**
    - **Validates: Requirements 11.1, 11.2**

  - [x] 2.7 Write unit tests for `SavedDishHandler`
    - Test all handler methods per the Testing Strategy in the design (CreateAsync, UpdateAsync, DeleteAsync, GetSuggestionsAsync edge cases)
    - _Requirements: 1.3, 1.4, 1.5, 1.6, 5.2, 5.3, 6.1, 11.1, 11.2, 11.6_

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement `SavedDishesFunction` API endpoints and extend `DaysFunction`
  - [x] 4.1 Create shared contracts
    - Create `Happie.Shared/Contracts/SavedDishDto.cs`, `CreateSavedDishRequest.cs`, `UpdateSavedDishRequest.cs`, `SavedDishSuggestionDto.cs`
    - Add `DishAlreadyExists` constant to `ApiErrorCodes.cs`
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5_

  - [x] 4.2 Create `SavedDishesFunction`
    - Implement `GET /api/saved-dishes` → returns active saved dishes sorted alphabetically
    - Implement `POST /api/saved-dishes` → creates/reactivates, returns 201/409/422
    - Implement `PUT /api/saved-dishes/{id}` → updates description, returns 200/400/404/409/422
    - Implement `DELETE /api/saved-dishes/{id}` → soft-deletes, returns 204/400/404
    - Implement `GET /api/saved-dishes/suggestions` → returns up to 5 suggestions
    - Use `RouteParser` for GUID validation, `RequestValidator` for body validation
    - _Requirements: 12.1–12.10_

  - [x] 4.3 Extend `DayHandler` with description resolution
    - In `GetDayPlanAsync`: when `DishRecord.SavedDishId` is non-null, resolve description from SavedDish; fall back to own description if not found
    - Extend `UpsertDishAsync` to accept optional `SavedDishId`; validate mutual exclusion (both set → 422, both null/empty → delete)
    - Update `DishDto` and `UpdateDishRequest` contracts with `SavedDishId` field
    - _Requirements: 2.2, 2.3, 2.5, 2.6, 2.7, 9.1, 9.2, 9.3, 9.5, 9.6, 9.7_

  - [x] 4.4 Write property test for description resolution
    - **Property 2: Description resolution correctness**
    - **Validates: Requirements 2.2, 2.3, 2.5, 2.6**

  - [x] 4.5 Write property test for dish save mutual exclusion
    - **Property 8: Dish save mutual exclusion**
    - **Validates: Requirements 9.6, 9.7**

  - [x] 4.6 Write unit tests for `SavedDishesFunction` and `DayHandler` extensions
    - Test function endpoint validation (invalid GUID, invalid body, etc.)
    - Test DayHandler description resolution (with/without SavedDishId, orphaned reference, soft-deleted reference)
    - Test DishRecordMapper extension for SavedDishId (Empty ↔ null mapping)
    - _Requirements: 2.2, 2.3, 2.5, 2.6, 9.6, 9.7, 12.8, 12.9_

- [x] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Frontend — SavedDishesPage
  - [x] 6.1 Create `SavedDishesPage` with add, edit, delete, and list
    - Create `Happie.Web/Pages/SavedDishesPage.razor` at route `/saved-dishes`
    - Display all active saved dishes sorted alphabetically
    - Implement Add_Button with reveal-on-click input field pattern
    - Implement inline edit with confirm (✓) and cancel (✗) buttons; confirm disabled when trimmed value equals current description
    - Implement soft-delete action with immediate list removal
    - Implement Highlight_Animation on newly added dish (scroll into view + animation)
    - Handle 409 conflict and network errors with localized messages
    - Block all operations when offline (`ConnectivityService.IsOnline` check with `Error_RequiresInternet` message)
    - All text via `IStringLocalizer<AppStrings>`
    - _Requirements: 3.1–3.14, 4.1–4.6, 13.1_

  - [x] 6.2 Implement suggestions section on SavedDishesPage
    - Display up to 5 suggestions below the saved dishes list
    - Tapping a suggestion calls `POST /api/saved-dishes` (same flow as promote)
    - Remove suggestion on success, show error on failure
    - Hide section when no suggestions available
    - _Requirements: 5.1–5.6_

  - [x] 6.3 Implement Explanation_Section
    - Always-visible section below suggestions explaining current and future benefits
    - Positioned at the bottom of page content
    - All text via `IStringLocalizer<AppStrings>`
    - _Requirements: 4.1–4.6_

  - [x] 6.4 Add localization keys for SavedDishesPage
    - Add all required keys to `AppStrings.resx` (Dutch) and `AppStrings.en.resx` (English)
    - Include: page title, add button, edit/delete labels, error messages, explanation text, suggestion labels, offline error
    - _Requirements: 3.14, 4.5, 5.6, 13.1, 14.4_

- [x] 7. Frontend — DishPanel mode toggle and SavedDishModal
  - [x] 7.1 Create `SavedDishModal` component
    - Create `Happie.Web/Components/SavedDishModal.razor`
    - Modal overlay with `role="dialog"`, z-index 1100/1101
    - Display alphabetically sorted list of active saved dishes as tappable items
    - Context-aware top action: "Use existing" when match found, "Add to saved" (Promote_Option) when no match
    - Hide Promote_Option when description is empty or exceeds 100 chars
    - Dismiss on backdrop tap or dismiss button
    - Add to swipe-guard selector in `index.html`
    - _Requirements: 8.3, 8.4, 8.5, 16.1, 16.2, 16.3, 16.7_

  - [x] 7.2 Extend `DishPanel` with Mode_Toggle
    - Add Mode_Toggle button adjacent to dish input on right side
    - Custom_Mode → activating toggle opens SavedDishModal
    - Saved_Mode → activating toggle switches back to Custom_Mode (pre-fill description)
    - In Saved_Mode: input field disabled, displays resolved saved dish description, visual distinction
    - Determine initial mode from DishRecord's `SavedDishId` value
    - Handle empty saved dishes state (localized empty message)
    - _Requirements: 8.1–8.10, 10.1–10.5_

  - [x] 7.3 Implement promote flow from DishPanel
    - On Promote_Option selection: `POST /api/saved-dishes`, switch to Saved_Mode on success
    - On 409: show localized message, switch to Saved_Mode with existing match
    - On network error: show localized error, remain in Custom_Mode
    - Save DishRecord with `SavedDishId` after successful promote
    - _Requirements: 16.3, 16.4, 16.5, 16.6_

  - [x] 7.4 Implement saved-mode dish save through CachedApiClient
    - Extend `CachedApiClient.SaveDishAsync` to accept optional `SavedDishId`
    - In saved mode: send `SavedDishId` with null description
    - Optimistic update includes resolved description in cached `DishDto`
    - Supports offline queueing via existing CachedApiClient mechanism
    - _Requirements: 9.1, 9.2, 9.4, 9.5, 13.2, 13.3, 13.4_

  - [x] 7.5 Add localization keys for DishPanel and SavedDishModal
    - Add keys for mode toggle, saved dish indicator, promote option, empty state, error messages
    - Add `Nav_SavedDishes` key for navigation
    - _Requirements: 10.4, 14.4, 16.1_

- [x] 8. Frontend — Navigation and visual indicator
  - [x] 8.1 Add "Saved Dishes" to NavMenu
    - Position as third item: Calendar → Saved Dishes → Housemates
    - Route to `/saved-dishes`
    - Active state when route starts with `saved-dishes`
    - `aria-label` via `IStringLocalizer<AppStrings>` with `Nav_SavedDishes` key
    - _Requirements: 14.1, 14.2, 14.3, 14.4_

  - [x] 8.2 Add saved dish visual indicator in DishPanel read mode
    - Display icon/badge when `SavedDishId` is non-null
    - Minimum 3:1 contrast ratio against both light and dark backgrounds
    - Include accessible `aria-label` via `IStringLocalizer<AppStrings>`
    - No indicator when `SavedDishId` is null (existing behavior)
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_

- [x] 9. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Future feature prompt documents
  - [x] 10.1 Create prompt documents for planned features
    - Create `prompt-public-dishes.md` describing toggle-to-public, cross-household suggestions, copy semantics
    - Create `prompt-saved-dish-history.md` describing audit trail following DayHistoryEntry pattern
    - Create `prompt-recipes.md` describing ingredients list and cooking instructions on SavedDish
    - Create `prompt-statistics.md` describing per-dish frequency, per-housemate attribution, time ranges
    - Each document: one-paragraph summary, user story, key behaviors, affected components
    - _Requirements: 15.1–15.6_

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck (minimum 100 iterations)
- Unit tests validate specific examples and edge cases using xUnit
- All localized strings use `IStringLocalizer<AppStrings>` with keys in both `AppStrings.resx` (Dutch) and `AppStrings.en.resx` (English)
- The SavedDishesPage requires connectivity for all operations; saved-mode dish saves on DayPlan go through CachedApiClient for offline support
- Retroactive conversion runs synchronously within the POST response unless it would exceed 5 seconds

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.4", "1.5", "4.1"] },
    { "id": 1, "tasks": ["1.2", "1.3"] },
    { "id": 2, "tasks": ["1.6", "2.1"] },
    { "id": 3, "tasks": ["2.2", "2.3", "2.4", "2.5", "2.6", "2.7", "4.2", "4.3"] },
    { "id": 4, "tasks": ["4.4", "4.5", "4.6"] },
    { "id": 5, "tasks": ["6.1", "6.4", "7.1"] },
    { "id": 6, "tasks": ["6.2", "6.3", "7.2", "7.5", "8.1"] },
    { "id": 7, "tasks": ["7.3", "7.4", "8.2"] },
    { "id": 8, "tasks": ["10.1"] }
  ]
}
```
