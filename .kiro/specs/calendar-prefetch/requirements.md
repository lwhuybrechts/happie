# Requirements Document

## Introduction

The calendar-prefetch feature improves the perceived performance of the CalendarPage by preloading adjacent months (previous and next) in the background after the active month has loaded. This mirrors the existing adjacent-day preloading pattern on the DayPlanPage. The calendar cache limit is increased from 2 to 4 entries per household to accommodate the current month, adjacent months, and one extra slot for navigation. When navigating to a month whose data has not yet finished loading (cold cache with pending fetch), the CalendarPage displays a graceful loading state: the calendar grid renders with the correct days visible but without attendance dots, and a small loader indicator appears in the top-left corner to communicate that data is loading.

## Glossary

- **CalendarPage**: The Blazor page at `/calendar` that renders a month grid with attendance color dots for each day.
- **CalendarGrid**: The Blazor component that renders the calendar grid with day numbers and color dot indicators.
- **CachedApiClient**: The central API facade providing stale-while-revalidate caching for reads and offline queueing for writes.
- **CacheStore**: The service wrapping IndexedDB operations for storing and retrieving cached calendar and day plan responses.
- **Adjacent_Month**: The month immediately before or after the currently viewed month on the CalendarPage.
- **Prefetch**: A background fetch of calendar data for an adjacent month that fires after the active month has been loaded and rendered.
- **Graceful_Loading_State**: A UI state where the calendar grid structure (day numbers, week rows) is visible but attendance color dots are absent, combined with a loader indicator to signal that data is still loading.
- **Loader_Indicator**: The existing loading spinner in the mobile header / sidebar (driven by `LoadingIndicatorState`) that indicates background data fetching is in progress.
- **Cache_Limit**: The maximum number of calendar entries stored in IndexedDB per household before LRU eviction occurs.

## Requirements

### Requirement 1: Prefetch Adjacent Months

**User Story:** As a housemate, I want adjacent months to be preloaded in the background when I view the calendar, so that switching months feels instantaneous.

#### Acceptance Criteria

1. WHEN the CalendarPage has finished loading and rendering data for the active month, THE CalendarPage SHALL initiate background fetches for the previous month and the next month via CachedApiClient.
2. WHEN a prefetch completes successfully, THE CacheStore SHALL store the fetched calendar data in IndexedDB so that subsequent navigation to that month is served from cache without a network request.
3. WHEN the user navigates to an adjacent month whose data has already been prefetched, THE CalendarPage SHALL render the cached data without showing the graceful loading state, while a background revalidation fetch proceeds per the stale-while-revalidate pattern.
4. IF a prefetch network request fails, THEN THE CalendarPage SHALL silently discard the failure without showing any error to the user and without retrying the prefetch.
5. WHEN the user navigates to a different month before a prefetch completes, THE CalendarPage SHALL cancel any in-flight prefetch requests for the previously viewed month and initiate new prefetches for the new adjacent months.
6. WHILE the device is offline, THE CalendarPage SHALL skip prefetch requests entirely.
7. IF the adjacent month's data is already present in the CacheStore at the time prefetch would initiate, THEN THE CalendarPage SHALL skip the prefetch for that month.

### Requirement 2: Increase Calendar Cache Limit

**User Story:** As a housemate, I want the calendar cache to store enough months so that prefetched data is not immediately evicted, so that the preloading actually improves performance.

#### Acceptance Criteria

1. THE CacheStore SHALL allow a maximum of 6 calendar entries per household in IndexedDB.
2. THE CacheStore SHALL protect entries belonging to the today cluster (the month containing today's date, the month before it, and the month after it) from eviction regardless of navigation.
3. THE CacheStore SHALL protect entries belonging to the viewed cluster (the currently viewed month, the month before it, and the month after it) from eviction.
4. WHEN a new calendar entry is stored for a month not already present in the cache and the cache already contains 6 entries for the household, THE CacheStore SHALL evict the entry that is not in either protected cluster and whose month is farthest from the currently viewed month.
5. IF eviction cannot find an eligible entry to evict (all entries belong to a protected cluster), THEN THE CacheStore SHALL still store the new entry, allowing the cache to temporarily exceed the limit.

### Requirement 3: Graceful Loading State

**User Story:** As a housemate, I want to see the calendar grid structure immediately when navigating to a month that has not finished loading, so that the page does not feel broken or empty during slow connections.

#### Acceptance Criteria

1. WHEN the CalendarPage navigates to a month with no cached data and a network fetch is in progress, THE CalendarPage SHALL render the CalendarGrid with the correct days of the target month visible (day numbers and week structure) but without any attendance color dots, replacing the existing full-page loading text.
2. WHEN the CalendarPage is in the graceful loading state, THE CalendarPage SHALL activate the existing Loader_Indicator in the header (via `LoadingIndicatorState`) to communicate that data is being fetched.
3. WHEN the network fetch completes successfully, THE CalendarPage SHALL replace the graceful loading state with the full calendar data including attendance color dots and deactivate the Loader_Indicator (via `LoadingIndicatorState`).
4. IF the network fetch fails while in the graceful loading state, THEN THE CalendarPage SHALL transition to the existing error state with the retry button.
5. WHILE the CalendarPage is in the graceful loading state, THE CalendarGrid SHALL remain interactive (day buttons are clickable and navigate to the DayPlanPage).
6. IF the device is offline and the CalendarPage navigates to a month with no cached data, THEN THE CalendarPage SHALL display the existing offline no-data message instead of entering the graceful loading state.

### Requirement 4: Prefetch Scheduling

**User Story:** As a housemate, I want prefetches to not interfere with the active month loading, so that the calendar remains responsive.

#### Acceptance Criteria

1. WHEN the active month's data has been rendered on screen, THE CalendarPage SHALL defer prefetch initiation until at least one browser animation frame has elapsed, ensuring the render cycle is not blocked.
2. WHEN prefetches are initiated, THE CalendarPage SHALL issue the next month prefetch first and wait for it to complete or fail before issuing the previous month prefetch.
3. IF the first prefetch request (next month) fails, THEN THE CalendarPage SHALL still proceed to issue the previous month prefetch.
4. WHEN the user navigates away from the CalendarPage entirely, THE CalendarPage SHALL cancel any in-flight prefetch request and discard any queued prefetch that has not yet started.

