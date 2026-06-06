# Requirements Document

## Introduction

The offline-cache feature adds stale-while-revalidate caching and offline support to the Happie PWA. When a user visits a DayPlan page, cached data is shown instantly while a background refresh fetches the latest data from the server. A loading indicator signals when background refreshes are in progress. When the device is offline, users can browse cached data and continue making mutations (attendance, dish, comment changes) which are queued locally and synced when connectivity returns. An offline banner informs the user of the connectivity state. The implementation targets mobile-first usage and enforces cache size limits to respect device storage constraints.

## Glossary

- **Cache_Store**: A client-side IndexedDB database used to persist API response data (DayPlan, Calendar) keyed by request URL, enabling instant page loads and offline browsing.
- **Stale_While_Revalidate**: A caching strategy where cached data is served immediately to the UI, and a background network request fetches fresh data. If the fresh data differs from the cached version, both the cache and the UI are updated.
- **Background_Refresh**: A network request made in the background after cached data has been served, to check for newer data from the API.
- **Mutation_Queue**: A persistent queue (IndexedDB) of API write operations (POST, PUT, DELETE) that could not be sent because the device was offline. Queued mutations are replayed in order when connectivity is restored.
- **Offline_Banner**: A UI banner displayed at the top of the page when the device has no network connectivity, informing the user that data may be stale.
- **Loading_Indicator**: A spinning animation displayed while a Background_Refresh is in progress, indicating that fresh data is being fetched.
- **Cache_Entry**: A single record in the Cache_Store containing the response body, the request URL, and a timestamp indicating when it was stored.
- **Sync_Service**: A client-side service responsible for replaying queued mutations against the API when connectivity is restored and handling retry logic.

## Requirements

### Requirement 1: Stale-While-Revalidate Caching for DayPlan

**User Story:** As a housemate, I want to see the last-known DayPlan data instantly when I open a day, so that the page feels fast even on slow connections.

#### Acceptance Criteria

1. WHEN a user navigates to a DayPlan page and a Cache_Entry exists for that day, THE Cache_Store SHALL serve the cached DayPlanResponse to the UI within 100 milliseconds of navigation without waiting for a network response.
2. WHEN a user navigates to a DayPlan page and a Cache_Entry exists for that day, THE Cache_Store SHALL initiate a Background_Refresh to fetch the current DayPlanResponse from the API, with a timeout of 30 seconds.
3. WHEN a Background_Refresh completes successfully and the serialized JSON response differs from the stored Cache_Entry value, THE Cache_Store SHALL replace the stored Cache_Entry with the fresh response AND the UI SHALL update to reflect the new data.
4. WHEN a Background_Refresh completes successfully and the serialized JSON response is identical to the stored Cache_Entry value, THE Cache_Store SHALL update the Cache_Entry retrieval timestamp without triggering a UI re-render.
5. WHEN a user navigates to a DayPlan page and no Cache_Entry exists for that day, THE Cache_Store SHALL display a loading indicator, fetch the DayPlanResponse from the API, display it to the user upon success, and store it as a new Cache_Entry.
6. WHEN a user navigates to a DayPlan page (via swipe or direct navigation) and a Cache_Entry exists for that day, THE UI SHALL NOT display any "Loading..." text or loading skeleton; the cached data SHALL be rendered immediately so that day-to-day navigation feels seamless.
7. IF a Background_Refresh fails due to a network error or request timeout, THEN THE Cache_Store SHALL retain the existing Cache_Entry and not alter the UI.
8. IF a Background_Refresh fails due to an HTTP 401 response, THEN THE Cache_Store SHALL discard the cached session and redirect the user to the login page.
9. IF the initial fetch for a cold cache (no Cache_Entry) fails, THEN THE Cache_Store SHALL display an error message indicating the data could not be loaded and provide a retry option.

### Requirement 2: Stale-While-Revalidate Caching for Calendar

**User Story:** As a housemate, I want to see cached calendar data instantly when I open the calendar, so that I can quickly check dinner plans.

#### Acceptance Criteria

1. WHEN a user navigates to the Calendar page and a Cache_Entry exists for the calendar request, THE Cache_Store SHALL serve the cached CalendarResponse to the UI immediately without waiting for a network response.
2. WHEN a user navigates to the Calendar page and a Cache_Entry exists, THE Cache_Store SHALL initiate a Background_Refresh to fetch the current CalendarResponse from the API.
3. WHEN a Background_Refresh for the calendar completes successfully and the response differs from the cached data, THE Cache_Store SHALL replace the stored Cache_Entry with the fresh response AND the UI SHALL update to reflect the new data.
4. WHEN a Background_Refresh for the calendar completes successfully and the response is identical to the cached data, THE Cache_Store SHALL update the Cache_Entry timestamp without triggering a UI re-render.
5. WHEN a user navigates to the Calendar page and no Cache_Entry exists, THE Cache_Store SHALL fetch the CalendarResponse from the API, display it to the user, and store it as a new Cache_Entry.
6. IF a Background_Refresh for the calendar fails due to a network error, THEN THE Cache_Store SHALL retain the existing Cache_Entry and not alter the UI.

### Requirement 3: Background Loading Indicator

**User Story:** As a housemate, I want to see a subtle indicator when fresh data is being loaded in the background, so that I know the displayed data may not yet be up-to-date.

#### Acceptance Criteria

1. WHILE one or more Background_Refresh operations are in progress, THE Loading_Indicator SHALL be visible to the user.
2. WHEN all Background_Refresh operations have completed (success or failure) and the Loading_Indicator has been visible for at least 500 milliseconds, THE Loading_Indicator SHALL be hidden.
3. WHILE in mobile viewport (width below 768px), THE Loading_Indicator SHALL be rendered as a spinning animation positioned immediately to the right of the "HAPPIE" brand text in the mobile header bar, with a diameter of 16px.
4. WHILE in desktop viewport (width 768px or above), THE Loading_Indicator SHALL be rendered as a spinning animation positioned to the right of the "Happie" title text in the sidebar brand area, aligned to the right edge of the green background of the selected menu item, with a diameter of 16px.
5. THE Loading_Indicator SHALL use a CSS animation so that it does not block the main thread or interfere with user interactions.
6. IF the user has enabled `prefers-reduced-motion: reduce` in their operating system settings, THEN THE Loading_Indicator SHALL use a pulsing opacity animation instead of a spinning animation.
7. THE Loading_Indicator SHALL include an `aria-label` attribute with a localized description (resolved via `IStringLocalizer<AppStrings>`) so that screen readers announce the loading state.

### Requirement 4: Cache Size Management

**User Story:** As a housemate using a mobile device, I want the app to limit how much storage it uses for cached data, so that it does not consume excessive device storage.

#### Acceptance Criteria

1. THE Cache_Store SHALL retain at most 30 DayPlan Cache_Entries at any time, each keyed by its calendar date.
2. WHEN storing a new DayPlan Cache_Entry would exceed the 30-entry limit, THE Cache_Store SHALL evict the Cache_Entry whose last-read-or-written timestamp is oldest before storing the new one.
3. THE Cache_Store SHALL retain at most 2 Calendar Cache_Entries: one for the current month and one for the last visited other month.
4. WHEN a new Calendar Cache_Entry is stored for a month other than the current month, THE Cache_Store SHALL replace the previously stored non-current-month Calendar Cache_Entry.
5. THE Cache_Store SHALL use IndexedDB for persistence so that cached data survives browser tab closures and app restarts.
6. IF IndexedDB is unavailable or a storage operation fails, THEN THE Cache_Store SHALL continue operating without caching and SHALL NOT display an error to the user.

### Requirement 5: Offline Browsing

**User Story:** As a housemate with no internet connection, I want to browse previously loaded day plans, so that I can check dinner information without connectivity.

#### Acceptance Criteria

1. WHILE the device has no network connectivity and a Cache_Entry exists for the requested DayPlan, THE Cache_Store SHALL serve the cached DayPlanResponse to the UI.
2. WHILE the device has no network connectivity and a Cache_Entry exists for the calendar, THE Cache_Store SHALL serve the cached CalendarResponse to the UI.
3. WHILE the device has no network connectivity and no Cache_Entry exists for the requested page (whether navigated via direct URL, swipe gesture, or calendar tap), THE UI SHALL display a localized message indicating that no cached data is available for this page (resolved via `IStringLocalizer<AppStrings>`).
4. WHILE the device has no network connectivity, THE UI SHALL not attempt Background_Refresh requests.
5. WHILE the device has no network connectivity, swipe navigation between days SHALL function normally, serving cached DayPlanResponses for adjacent days when available.

### Requirement 6: Offline Mutation Queueing

**User Story:** As a housemate with no internet connection, I want to continue making changes (attendance, dish, comments), so that I do not have to wait for connectivity to interact with the app.

#### Acceptance Criteria

1. WHILE the device has no network connectivity, WHEN the user performs a mutation (attendance change, dish save, comment save, or comment delete), THE Mutation_Queue SHALL store the mutation request (HTTP method, URL, headers including Authorization and X-Housemate-Id, body) in IndexedDB in the order it was performed.
2. WHILE the device has no network connectivity, THE UI SHALL apply the mutation optimistically to the in-memory state and the cached DayPlanResponse in the Cache_Store, consistent with the existing optimistic UI pattern.
3. WHEN network connectivity is restored, THE Sync_Service SHALL begin replaying queued mutations against the API in the order they were enqueued within 5 seconds of the `online` event.
4. WHEN a queued mutation is replayed successfully (HTTP 2xx response), THE Sync_Service SHALL remove it from the Mutation_Queue.
5. IF a replayed mutation fails with an HTTP 4xx client error (validation error, conflict), THEN THE Sync_Service SHALL discard the failed mutation from the queue, roll back the optimistic change in the Cache_Store, and display a localized toast notification informing the user that the offline change could not be saved (resolved via `IStringLocalizer<AppStrings>`).
6. IF a replayed mutation fails with an HTTP 5xx server error or a network error, THEN THE Sync_Service SHALL retain the mutation in the queue and retry with exponential backoff (initial delay 2 seconds, maximum delay 60 seconds, maximum 5 retry attempts).
7. IF a queued mutation exhausts all retry attempts, THEN THE Sync_Service SHALL discard the mutation, roll back the optimistic change in the Cache_Store, and display a localized toast notification informing the user that the offline change could not be saved (resolved via `IStringLocalizer<AppStrings>`).
8. THE Mutation_Queue SHALL persist across browser tab closures and app restarts so that queued mutations are not lost.
9. THE Sync_Service SHALL replay all queued mutations sequentially without deduplication; if two mutations target the same resource, both SHALL be replayed in order and any resulting conflict SHALL be handled per criteria 5.
10. WHEN a queued mutation is replayed, the client SHALL include an `If-Unmodified-Since` header containing the timestamp at which the mutation was originally performed (when the user made the change offline).
11. WHEN the server receives a mutation with an `If-Unmodified-Since` header and the resource (attendance record, dish record, or comment) has been modified after that timestamp, THE server SHALL reject the mutation with HTTP 409 `CONFLICT`.
12. WHEN the server receives a mutation with an `If-Unmodified-Since` header and the resource has NOT been modified after that timestamp (or does not yet exist), THE server SHALL apply the mutation normally.
13. WHEN a replayed mutation is rejected with HTTP 409 due to a concurrent modification, THE Sync_Service SHALL discard the mutation, roll back the optimistic change in the Cache_Store, and display a localized toast notification informing the user that their offline change was not applied because another housemate made a more recent change (resolved via `IStringLocalizer<AppStrings>`).

### Requirement 7: Offline Banner

**User Story:** As a housemate, I want to see a clear indication when I am offline, so that I know my data may be stale and my changes will be synced later.

#### Acceptance Criteria

1. WHEN the device loses network connectivity, THE Offline_Banner SHALL appear at the top of the page within 1 second of the connectivity change. WHEN the Offline_Banner component initializes and `navigator.onLine` is `false`, THE Offline_Banner SHALL appear immediately without waiting for an `offline` event.
2. WHEN the device regains network connectivity, THE Offline_Banner SHALL be hidden within 1 second of the connectivity change.
3. THE Offline_Banner SHALL display a localized message indicating that the user is offline and that changes will sync when connectivity returns (resolved via `IStringLocalizer<AppStrings>`).
4. THE Offline_Banner SHALL be rendered with a fixed position so that it remains visible while scrolling.
5. THE Offline_Banner SHALL use a z-index value of 1050 so that it renders above the mobile header (z-index 1000) but below modal overlays (z-index 1100).
6. THE Offline_Banner SHALL use `navigator.onLine` and the `online`/`offline` window events as the connectivity detection mechanism.
7. WHILE the user is on the login page, THE Offline_Banner SHALL NOT be displayed, because cached data is only available after authentication.

### Requirement 8: Cache Invalidation on Successful Mutation

**User Story:** As a housemate, I want the cache to stay up-to-date after I make a change, so that I see my own changes reflected immediately when revisiting a page.

#### Acceptance Criteria

1. WHEN a mutation (attendance, dish, comment) is saved successfully to the API (while online), THE Cache_Store SHALL update the corresponding DayPlan Cache_Entry with the optimistically applied state.
2. WHEN an attendance mutation is saved successfully for a day that is present in a cached Calendar response, THE Cache_Store SHALL update that Calendar Cache_Entry in-place to reflect the user's attendance change (add or remove the housemate's color dot for that day) rather than invalidating the entire entry.
3. WHEN the user logs out or the JWT expires (detected on any API response returning HTTP 401), THE Cache_Store SHALL clear all Cache_Entries and the Mutation_Queue to prevent stale data from being shown to a different user or session.
4. WHEN a successful login occurs, THE Cache_Store SHALL clear all Cache_Entries and the Mutation_Queue for any previously stored HouseholdId that differs from the newly authenticated HouseholdId, to prevent cross-household data leakage from push notification deep-links.
5. WHEN a mutation is saved successfully for a day that has no existing DayPlan Cache_Entry, THE Cache_Store SHALL NOT create a new Cache_Entry for that day.

### Requirement 9: Cache Scoping by Household

**User Story:** As a housemate, I want cached data to be isolated per household, so that switching households does not show incorrect data.

#### Acceptance Criteria

1. THE Cache_Store SHALL scope all Cache_Entries by the HouseholdId extracted from the active session JWT.
2. WHEN the active HouseholdId changes (e.g., user logs in to a different household), THE Cache_Store SHALL clear all Cache_Entries associated with the previous HouseholdId.
3. THE Mutation_Queue SHALL scope all queued mutations by the HouseholdId from the active session JWT.
4. WHEN the active HouseholdId changes and the Mutation_Queue contains unsynced mutations for the previous HouseholdId, THE Sync_Service SHALL discard those mutations without replay and SHALL NOT display an error to the user.

### Requirement 10: Sync Status Feedback

**User Story:** As a housemate, I want to know when my offline changes have been synced, so that I have confidence my changes were saved.

#### Acceptance Criteria

1. WHEN the Sync_Service begins replaying queued mutations after connectivity is restored, THE Loading_Indicator SHALL be visible to indicate sync activity.
2. WHEN the Sync_Service has finished processing all queued mutations (whether by successful replay, permanent failure, or exhausted retries), THE Loading_Indicator SHALL be hidden.
3. IF one or more queued mutations fail permanently (exhausted retries or client error), THEN THE UI SHALL display a localized toast notification per failed mutation (maximum 3 visible simultaneously, additional toasts queued until a slot is available) that includes the mutation type (attendance, dish, or comment) and the target date, resolved via `IStringLocalizer<AppStrings>`.
4. WHEN a sync failure toast notification is displayed, THE UI SHALL auto-dismiss it after 8 seconds, or WHEN the user manually dismisses it by tapping the toast's close control.
