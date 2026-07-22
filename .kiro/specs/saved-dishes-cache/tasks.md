# Implementation Plan: Saved Dishes Cache

## Overview

This plan extends the existing IndexedDB-backed offline cache to include the household's saved dishes list. A new `savedDishCache` object store is added to `cacheDb.js` (DB version bump 1→2), with matching C# interop methods in `ICacheStore`/`CacheStore` and a new `GetSavedDishesAsync` method on `ICachedApiClient` implementing stale-while-revalidate. The `DishPanel` and `SavedDishesPage` switch from direct `HttpClient` calls to the cached facade, enabling offline access and instant modal opens. The promote action is disabled offline, and cache invalidation uses refetch-and-replace after mutations.

## Tasks

- [x] 1. IndexedDB object store and JS interop
  - [x] 1.1 Add `savedDishCache` object store to `cacheDb.js`
    - Bump `DB_VERSION` from 1 to 2
    - Add `savedDishCache` object store creation in `onupgradeneeded` (keyPath: `"key"`, index: `byHousehold` on `householdId`)
    - Handle upgrade from v1 (only create `savedDishCache` if it doesn't exist, preserve existing stores)
    - Add `getSavedDishes(householdId)` function — returns cached entry or null
    - Add `putSavedDishes(householdId, responseJson, timestamp)` function — upserts the entry
    - Add `deleteSavedDishes(householdId)` function — deletes the entry by key
    - Update `clearAll(householdId)` to also clear `savedDishCache` entries for the household
    - Expose new functions on `window.happieCache`
    - _Requirements: 5.1, 5.2, 5.4, 3.3_

- [x] 2. CacheStore C# wrapper methods
  - [x] 2.1 Create `CachedSavedDishes` record and add methods to `ICacheStore`/`CacheStore`
    - Create `CachedSavedDishes` record in `Happie.Web/Services/Caching/` — `CachedSavedDishes(string ResponseJson, long Timestamp)`
    - Add `Task<CachedSavedDishes?> GetSavedDishesAsync(string householdId)` to `ICacheStore`
    - Add `Task PutSavedDishesAsync(string householdId, string responseJson)` to `ICacheStore`
    - Add `Task DeleteSavedDishesAsync(string householdId)` to `ICacheStore`
    - Implement methods in `CacheStore` with JS interop calls to `window.happieCache.getSavedDishes` / `putSavedDishes` / `deleteSavedDishes`
    - Follow existing pattern: guard with `_isAvailable` check, catch `JSException`
    - _Requirements: 5.2, 5.3_

- [x] 3. CachedApiClient saved dishes methods
  - [x] 3.1 Add `GetSavedDishesAsync` with stale-while-revalidate to `ICachedApiClient`/`CachedApiClient`
    - Create `SavedDishesFetchResult` record — `SavedDishesFetchResult(IReadOnlyList<SavedDishDto>? Dishes, bool IsColdCache, bool HasError)`
    - Add `Task<SavedDishesFetchResult> GetSavedDishesAsync()` to `ICachedApiClient`
    - Implement stale-while-revalidate: check cache → return cached immediately; if online fire background refresh; if cold cache + online fetch and cache; if cold cache + offline return null with `IsColdCache = true`
    - _Requirements: 1.1, 1.2, 1.5, 2.1, 2.2, 2.3_

  - [x] 3.2 Add background refresh and event notification
    - Add `event Action<IReadOnlyList<SavedDishDto>>? OnSavedDishesUpdated` to `ICachedApiClient`
    - Implement `BackgroundRefreshSavedDishesAsync` — fetch from API, compare, update cache, fire `OnSavedDishesUpdated` if changed; update timestamp only if identical
    - Handle 401 in background refresh (clear session + redirect)
    - Handle network failure (retain existing cache, no UI change)
    - _Requirements: 1.3, 1.4, 1.7, 1.8_

  - [x] 3.3 Add `RefreshSavedDishesCacheAsync` for mutation invalidation
    - Add `Task RefreshSavedDishesCacheAsync()` to `ICachedApiClient`
    - Implement refetch-and-replace: fetch from API, replace cache entry, fire `OnSavedDishesUpdated`
    - If refetch fails, delete cache entry so next access triggers fresh fetch
    - _Requirements: 3.1, 3.2_

  - [x] 3.4 Write property test for cache read round-trip
    - **Property 1: Cache read round-trip**
    - **Validates: Requirements 1.1, 2.1, 4.1, 5.4**

  - [x] 3.5 Write property test for background refresh replaces cache
    - **Property 2: Background refresh replaces cache when data differs**
    - **Validates: Requirements 1.3, 4.2**

  - [x] 3.6 Write property test for cold cache fetch stores and returns data
    - **Property 3: Cold cache fetch stores and returns data**
    - **Validates: Requirements 1.5, 4.3**

  - [x] 3.7 Write property test for network failure preserves cache
    - **Property 4: Network failure preserves cache**
    - **Validates: Requirements 1.7**

  - [x] 3.8 Write property test for refetch-and-replace after mutation
    - **Property 5: Refetch-and-replace after successful mutation**
    - **Validates: Requirements 3.1, 4.4**

  - [x] 3.9 Write property test for cache invalidation on refetch failure
    - **Property 6: Cache invalidation on refetch failure**
    - **Validates: Requirements 3.2**

  - [x] 3.10 Write property test for single entry per household invariant
    - **Property 7: Single entry per household invariant**
    - **Validates: Requirements 5.1**

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Pre-population trigger
  - [x] 5.1 Add pre-population in `CachedApiClient.GetDayPlanAsync`
    - After serving a day plan (cached or fresh), check if saved dishes cache exists for the household
    - If no saved dishes cache entry exists and device is online, fire-and-forget a background fetch of `GET /api/saved-dishes` and store in cache
    - Use a boolean flag `_savedDishesPrePopulated` to ensure this only runs once per session
    - Ensure the background fetch does not block navigation or delay the DayPlan page render
    - _Requirements: 6.1, 6.2, 6.3_

- [x] 6. Update `DishPanel.razor` to use cached saved dishes
  - [x] 6.1 Replace direct HTTP call with cached API facade
    - Replace `Http.GetAsync("saved-dishes")` in `OpenSavedDishModalAsync` with `CachedApi.GetSavedDishesAsync()`
    - Handle `SavedDishesFetchResult`: `Dishes` not null → open modal with cached/fresh data; `HasError` true → show existing error message (`SavedDish_LoadError`); `IsColdCache` + offline → show localized "not available offline" message
    - Remove direct `HttpClient` injection from `DishPanel` if it is no longer used for any other calls (verify first)
    - _Requirements: 1.1, 1.5, 1.6, 2.1, 2.2_

- [x] 7. Disable promote action offline in `SavedDishModal.razor`
  - [x] 7.1 Disable promote button when offline
    - Inject `IConnectivityService` into `SavedDishModal`
    - Disable the promote button when `!ConnectivityService.IsOnline`
    - Add a localized tooltip or inline note below the promote button when offline
    - _Requirements: 2.4_

- [x] 8. Update `SavedDishesPage.razor` to use cached saved dishes
  - [x] 8.1 Replace direct HTTP call with cached API facade
    - Replace `Http.GetAsync("saved-dishes")` in `LoadDishesAsync` with `CachedApi.GetSavedDishesAsync()`
    - Subscribe to `OnSavedDishesUpdated` event in `OnInitializedAsync`; unsubscribe in `Dispose`
    - Handle background refresh updates: if the page's dish list is not being actively edited, update from the event
    - _Requirements: 4.1, 4.2_

  - [x] 8.2 Add cache invalidation after mutations
    - After successful create (`PostAsync`): call `await CachedApi.RefreshSavedDishesCacheAsync()`
    - After successful rename (`PutAsync`): call `await CachedApi.RefreshSavedDishesCacheAsync()`
    - After successful delete (`DeleteAsync`): call `await CachedApi.RefreshSavedDishesCacheAsync()`
    - _Requirements: 3.1, 4.4_

  - [x] 8.3 Disable mutation actions when offline
    - Inject `IConnectivityService` for connectivity checks
    - When offline: show cached list as read-only, disable add/rename/delete buttons with localized `Error_RequiresInternet` message
    - _Requirements: 4.5_

- [x] 9. Session cleanup verification
  - [x] 9.1 Verify `clearAll` covers saved dishes cache
    - Verify that `CacheStore.ClearAllAsync` now clears `savedDishCache` entries (covered by task 1.1's `clearAll` JS update + task 2.1's C# wrapper)
    - Verify that the 401 handling path in `CachedApiClient` (which calls `ClearAllAsync`) also clears the saved dishes cache
    - Verify that login with a different household clears the previous household's saved dishes cache
    - _Requirements: 3.3, 3.4_

- [x] 10. Localization keys
  - [x] 10.1 Add localization keys for offline messages
    - Add `SavedDish_OfflineUnavailable` to `AppStrings.resx` (Dutch) — "Opgeslagen gerechten zijn niet beschikbaar zonder internet."
    - Add `SavedDish_OfflineUnavailable` to `AppStrings.en.resx` (English) — "Saved dishes are not available offline."
    - Add `SavedDish_PromoteOffline` to `AppStrings.resx` (Dutch) — "Opslaan als gerecht vereist internet."
    - Add `SavedDish_PromoteOffline` to `AppStrings.en.resx` (English) — "Saving as a dish requires internet."
    - _Requirements: 2.2, 2.4_

- [x] 11. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 12. Unit tests for offline and edge-case scenarios
  - [x] 12.1 Write unit tests for offline behavior
    - Test cold cache + offline shows "not available offline" message (Requirement 2.2)
    - Test background refresh is not attempted when offline (Requirement 2.3)
    - Test promote button disabled when offline (Requirement 2.4)
    - Test 401 response triggers session clear and redirect (Requirement 1.8)
    - _Requirements: 1.8, 2.2, 2.3, 2.4_

  - [x] 12.2 Write unit tests for cache lifecycle
    - Test identical response only updates timestamp, not data (Requirement 1.4)
    - Test cache cleared on logout/clearAll (Requirement 3.3)
    - Test cache cleared on household ID change (Requirement 3.4)
    - Test pre-population triggered on first DayPlan load (Requirement 6.1)
    - Test pre-population failure does not show error (Requirement 6.2)
    - Test pre-population does not block DayPlan render (Requirement 6.3)
    - _Requirements: 1.4, 3.3, 3.4, 6.1, 6.2, 6.3_

  - [x] 12.3 Write unit tests for fallback and page behavior
    - Test IndexedDB unavailability falls back to direct API calls (Requirement 5.3)
    - Test SavedDishesPage disables mutations when offline (Requirement 4.5)
    - _Requirements: 4.5, 5.3_

- [x] 13. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- No server-side changes are needed — the existing `GET /api/saved-dishes` endpoint is reused as-is
- The DB version bump from 1→2 is handled transparently by IndexedDB's upgrade mechanism; existing stores are preserved
- The `clearAll` update in task 1.1 ensures session cleanup covers the new store immediately

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["3.1", "10.1"] },
    { "id": 3, "tasks": ["3.2", "3.3"] },
    { "id": 4, "tasks": ["3.4", "3.5", "3.6", "3.7", "3.8", "3.9", "3.10", "5.1"] },
    { "id": 5, "tasks": ["6.1", "7.1", "8.1"] },
    { "id": 6, "tasks": ["8.2", "8.3", "9.1"] },
    { "id": 7, "tasks": ["12.1", "12.2", "12.3"] }
  ]
}
```
