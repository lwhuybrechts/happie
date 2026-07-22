# Requirements Document

## Introduction

The saved-dishes-cache feature extends the existing offline cache architecture to include the household's saved dishes list. Currently, the `SavedDishModal` (opened from the DayPlan page's DishPanel) fetches saved dishes from the API on every open, which fails when the device is offline — leaving the user unable to select a saved dish. By caching the saved dishes list in IndexedDB and using the same stale-while-revalidate pattern as DayPlan and Calendar data, the modal opens instantly from cache and stays usable offline. Additionally, the `SavedDishesPage` benefits from the same cache, showing the list immediately on navigation.

## Glossary

- **Saved_Dishes_Cache**: A dedicated IndexedDB object store that persists the household's active saved dishes list (the response from `GET /api/saved-dishes`), enabling instant retrieval and offline access.
- **Stale_While_Revalidate**: The existing caching strategy where cached data is served immediately, and a background network request fetches fresh data. If the fresh response differs, both the cache and the UI are updated.
- **Background_Refresh**: A network request made in the background after cached saved dishes are served, to check for newer data from the API.
- **Cache_Invalidation**: The process of updating the cached saved dishes list when the user creates, renames, or deletes a saved dish (either online or via offline mutation replay).
- **Optimistic_Update**: Immediately applying a mutation to the cached saved dishes list before the API response confirms success, rolling back on failure.

## Requirements

### Requirement 1: Stale-While-Revalidate Caching for Saved Dishes

**User Story:** As a housemate, I want the saved dish modal to open instantly with my household's saved dishes, so that selecting a dish feels fast and works even on slow connections.

#### Acceptance Criteria

1. WHEN the `SavedDishModal` is opened (via DishPanel bookmark button) and a Saved_Dishes_Cache entry exists for the current household, THE cached saved dishes list SHALL be served to the modal immediately without waiting for a network response.
2. WHEN a Saved_Dishes_Cache entry exists and the device is online, THE system SHALL initiate a Background_Refresh to fetch the current saved dishes list from `GET /api/saved-dishes`.
3. WHEN a Background_Refresh completes successfully and the response differs from the cached data, THE Saved_Dishes_Cache SHALL be updated with the fresh response AND the UI SHALL NOT re-render the already-open modal (to avoid disrupting the user's selection), but subsequent modal opens SHALL use the fresh data.
4. WHEN a Background_Refresh completes successfully and the response is identical to the cached data, THE Saved_Dishes_Cache SHALL update its timestamp without any UI change.
5. WHEN the `SavedDishModal` is opened and no Saved_Dishes_Cache entry exists (cold cache) and the device is online, THE system SHALL fetch the saved dishes list from the API, display it in the modal upon success, and store it in the Saved_Dishes_Cache.
6. WHEN the `SavedDishModal` is opened with a cold cache and the API request fails, THE modal SHALL display a localized error message indicating that saved dishes could not be loaded (using the existing `SavedDish_LoadError` key via `IStringLocalizer<AppStrings>`).
7. IF a Background_Refresh fails due to a network error, THEN THE Saved_Dishes_Cache SHALL retain the existing entry and not alter the UI.
8. IF a Background_Refresh fails due to an HTTP 401 response, THEN THE system SHALL clear the session and redirect to the login page (consistent with existing `CachedApiClient` behavior).

### Requirement 2: Offline Access to Saved Dishes

**User Story:** As a housemate with no internet connection, I want to open the saved dish modal and select dishes, so that I can complete my day plan even when offline.

#### Acceptance Criteria

1. WHILE the device has no network connectivity and a Saved_Dishes_Cache entry exists, THE `SavedDishModal` SHALL open with the cached saved dishes list, allowing the user to select and confirm dishes.
2. WHILE the device has no network connectivity and no Saved_Dishes_Cache entry exists, THE `SavedDishModal` SHALL display a localized message indicating that saved dishes are not available offline and no cached data exists.
3. WHILE the device has no network connectivity, THE system SHALL NOT attempt a Background_Refresh for saved dishes.
4. WHILE the device has no network connectivity, the "promote" action (saving a custom description as a new saved dish) within the `SavedDishModal` SHALL be disabled, because it requires connectivity and is not part of the offline mutation queue.

### Requirement 3: Cache Invalidation on Saved Dish Mutations

**User Story:** As a housemate, I want the cached saved dishes list to stay up-to-date after I add, rename, or delete a dish, so that the modal always reflects my latest changes.

#### Acceptance Criteria

1. WHEN a saved dish mutation (create, rename, or delete) completes successfully, THE system SHALL refetch the full saved dishes list from `GET /api/saved-dishes` and replace the Saved_Dishes_Cache entry with the fresh response.
2. WHEN the refetch after a successful mutation fails (network error), THE Saved_Dishes_Cache SHALL be invalidated (deleted) so that the next access triggers a fresh fetch rather than serving stale data.
3. WHEN the user logs out or the session expires (HTTP 401), THE Saved_Dishes_Cache SHALL be cleared along with all other cache entries (consistent with existing `ClearAllAsync` behavior).
4. WHEN a successful login occurs with a different HouseholdId than previously stored, THE Saved_Dishes_Cache SHALL be cleared to prevent cross-household data leakage.

### Requirement 4: SavedDishesPage Cache Integration

**User Story:** As a housemate, I want the Saved Dishes page to load instantly from cache, so that navigation feels seamless.

#### Acceptance Criteria

1. WHEN the user navigates to the SavedDishesPage and a Saved_Dishes_Cache entry exists, THE page SHALL display the cached list immediately without waiting for a network response.
2. WHEN a Saved_Dishes_Cache entry exists and the device is online, THE page SHALL initiate a Background_Refresh and update the displayed list if the fresh response differs.
3. WHEN the SavedDishesPage is loaded with a cold cache and the device is online, THE page SHALL fetch the saved dishes list from the API, display it, and store it in the Saved_Dishes_Cache.
4. WHEN the user performs add, rename, or delete operations on the SavedDishesPage, THE Saved_Dishes_Cache SHALL be updated optimistically (per Requirement 3) so that both the page and subsequent modal opens reflect the change immediately.
5. WHILE the device has no network connectivity, THE SavedDishesPage SHALL display the cached list (read-only browsing) and disable add/rename/delete actions that require connectivity, showing a localized "requires internet" message.

### Requirement 5: Cache Storage and Limits

**User Story:** As a housemate, I want the saved dishes cache to use minimal storage, so that it does not contribute to excessive device storage usage.

#### Acceptance Criteria

1. THE Saved_Dishes_Cache SHALL store at most 1 entry per household (the full list of active saved dishes from `GET /api/saved-dishes`).
2. THE Saved_Dishes_Cache SHALL use IndexedDB for persistence so that cached data survives browser tab closures and app restarts.
3. IF IndexedDB is unavailable or a storage operation fails, THEN THE system SHALL continue operating without caching saved dishes (fallback to direct API calls when online) and SHALL NOT display an error to the user.
4. THE Saved_Dishes_Cache entry SHALL include the HouseholdId and a timestamp to support scoping and staleness checks.

### Requirement 6: Pre-population on Login

**User Story:** As a housemate, I want the saved dishes list to be cached shortly after I log in, so that the modal is ready when I first need it.

#### Acceptance Criteria

1. WHEN the user completes login and navigates to the first DayPlan page, THE system SHALL trigger a background fetch of `GET /api/saved-dishes` and store the result in the Saved_Dishes_Cache, so that subsequent modal opens are instant.
2. IF the background pre-population fetch fails, THEN THE system SHALL NOT display an error; the cache will be populated on the first modal open instead.
3. THE background pre-population SHALL NOT block navigation or delay the DayPlan page render.
