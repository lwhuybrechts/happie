# Implementation Plan: Calendar Prefetch

## Overview

This plan implements adjacent-month preloading on the CalendarPage, increases the calendar cache limit from 2 to 6 with cluster-based eviction protection, adds a graceful loading state for cold cache navigations, and refactors loading indicator management from CachedApiClient to pages. The implementation starts with new result types and interface changes, then modifies the cache eviction logic, then updates CalendarPage and DayPlanPage to use the new patterns.

## Tasks

- [x] 1. Create result record types and update interfaces
  - [x] 1.1 Create `CalendarFetchResult` and `DayPlanFetchResult` records
    - Create `CalendarFetchResult.cs` in `Happie.Web/Services/Caching/` with properties: `CalendarResponse? Data`, `bool IsColdCacheFetch`, `bool HasLoadError`, `Task? BackgroundRefreshTask`
    - Create `DayPlanFetchResult.cs` in `Happie.Web/Services/Caching/` with properties: `DayPlanResponse? Data`, `bool IsColdCacheFetch`, `bool HasLoadError`, `Task? BackgroundRefreshTask`
    - _Requirements: 3.2, 3.3_

  - [x] 1.2 Update `ICachedApiClient` interface to return new result types
    - Change `GetCalendarAsync` return type from `Task<CalendarResponse?>` to `Task<CalendarFetchResult>`
    - Change `GetDayPlanAsync` return type from `Task<DayPlanResponse?>` to `Task<DayPlanFetchResult>`
    - Remove `IsColdCacheFetch` and `HasLoadError` properties from the interface (state is now carried in the result record)
    - _Requirements: 3.2, 3.3_

  - [x] 1.3 Update `ICacheStore` interface with new `PutCalendarAsync` signature
    - Change `PutCalendarAsync` signature to accept `string viewedMonth` parameter for cluster-based eviction
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

- [x] 2. Implement cache eviction changes
  - [x] 2.1 Add `getEvictableCalendarKey` to `cacheDb.js`
    - Add method `getEvictableCalendarKey(householdId, todayMonth, viewedMonth)` to `window.happieCache`
    - Method reads all calendar entries for the household, filters out entries in the today cluster (today ± 1 month) and viewed cluster (viewed ± 1 month), and returns the key of the entry farthest from viewedMonth
    - Returns null if no eligible entry exists
    - _Requirements: 2.2, 2.3, 2.4_

  - [x] 2.2 Update `CacheStore.PutCalendarAsync` with cluster-based eviction
    - Change `MaxCalendarEntries` from `2` to `6`
    - Update `PutCalendarAsync` to accept the `viewedMonth` parameter
    - Replace old eviction logic (first non-current-month entry) with cluster-based protection: call `getEvictableCalendarKey` JS interop method when entry count ≥ 6 and the new month is not already cached
    - If `getEvictableCalendarKey` returns null (all entries in protected clusters), allow cache to temporarily exceed limit
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 2.3 Write property test for calendar cache cluster-based eviction
    - **Property 1: Calendar cache enforces 6-entry limit with cluster-based protection**
    - **Validates: Requirements 2.1, 2.2, 2.3, 2.4**

- [x] 3. Update CachedApiClient to return result types
  - [x] 3.1 Refactor `CachedApiClient.GetCalendarAsync` to return `CalendarFetchResult`
    - Return `CalendarFetchResult` with `Data`, `IsColdCacheFetch`, `HasLoadError`, and `BackgroundRefreshTask`
    - For cache hits: set `BackgroundRefreshTask` to the background refresh task (instead of fire-and-forget `_ =`)
    - For cold cache fetches: set `BackgroundRefreshTask` to the fetch task so the caller can await it
    - Remove `IsColdCacheFetch` and `HasLoadError` instance property assignments
    - Pass `viewedMonth` to `CacheStore.PutCalendarAsync` calls
    - _Requirements: 1.1, 1.2, 1.3, 3.1, 3.2, 3.3_

  - [x] 3.2 Refactor `CachedApiClient.GetDayPlanAsync` to return `DayPlanFetchResult`
    - Return `DayPlanFetchResult` with `Data`, `IsColdCacheFetch`, `HasLoadError`, and `BackgroundRefreshTask`
    - For cache hits: set `BackgroundRefreshTask` to the background refresh task
    - For cold cache fetches: set `BackgroundRefreshTask` to the fetch task
    - Remove `IsColdCacheFetch` and `HasLoadError` instance property assignments
    - _Requirements: 3.2, 3.3_

  - [x] 3.3 Remove `LoadingIndicatorState` calls from CachedApiClient background refresh methods
    - Remove `_loadingIndicatorState.IncrementAsync()` and `DecrementAsync()` calls from `BackgroundRefreshCalendarAsync` and `BackgroundRefreshDayPlanAsync`
    - Remove the `LoadingIndicatorState` dependency from `CachedApiClient` constructor if no other usages remain
    - _Requirements: 3.2_

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Implement CalendarPage prefetch and graceful loading
  - [x] 5.1 Add prefetch orchestration to CalendarPage
    - Add `CancellationTokenSource? _prefetchCts` field
    - Add `bool _isGracefulLoading` field
    - Add `PrefetchAdjacentMonthsAsync` method: fetch next month first, then previous month sequentially via `CachedApi.GetCalendarAsync`, discarding the `BackgroundRefreshTask` (fire-and-forget)
    - Skip prefetch if offline (`ConnectivityService.IsOnline` is false)
    - Skip prefetch for a month whose data is already cached
    - Cancel `_prefetchCts` on every new month navigation and on dispose
    - Catch `OperationCanceledException` in `PrefetchAdjacentMonthsAsync`
    - Use `await Task.Yield()` before starting prefetches to let the active month paint first
    - _Requirements: 1.1, 1.2, 1.4, 1.5, 1.6, 1.7, 4.1, 4.2, 4.3, 4.4_

  - [x] 5.2 Add graceful loading state to CalendarPage
    - Update `LoadCalendarDataAsync` to use the `CalendarFetchResult` return type
    - For cache hits: render immediately, then activate `LoadingIndicatorState` and await `BackgroundRefreshTask` (if non-null), deactivate on completion, then fire `PrefetchAdjacentMonthsAsync` as fire-and-forget
    - For cold cache + online: set `_isGracefulLoading = true`, set `_days = []` (empty list so CalendarGrid renders structure without dots), activate `LoadingIndicatorState`, await `BackgroundRefreshTask`, transition to full data or error state, deactivate indicator, then fire prefetch
    - For cold cache + offline: show offline message (no graceful loading)
    - Replace existing `_isLoading` full-page text with graceful loading state
    - Ensure day buttons remain clickable during graceful loading
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

  - [x] 5.3 Write unit tests for CalendarPage prefetch behavior
    - Test that after load, `GetCalendarAsync` is called for month+1 and month-1
    - Test that no prefetch calls are made when offline
    - Test that cancellation token is triggered on navigation
    - Test that next month is prefetched before previous month
    - Test that previous month prefetch still executes if next month fails
    - Test that prefetch failure does not show error UI
    - _Requirements: 1.1, 1.4, 1.5, 1.6, 4.2, 4.3_

  - [x] 5.4 Write unit tests for graceful loading state
    - Test cold cache + online renders CalendarGrid with empty Days and activates LoadingIndicatorState
    - Test fetch completion transitions from graceful loading to full data
    - Test fetch failure transitions to error state with retry button
    - Test day buttons are clickable during graceful loading
    - _Requirements: 3.1, 3.3, 3.4, 3.5_

- [x] 6. Update DayPlanPage for new return types
  - [x] 6.1 Update DayPlanPage to use `DayPlanFetchResult`
    - Update `LoadDayPlanDataAsync` to use the `DayPlanFetchResult` return type
    - Await `BackgroundRefreshTask` with `LoadingIndicatorState` active for the active date only
    - Discard `BackgroundRefreshTask` for adjacent-day prefetches (fire-and-forget, no indicator)
    - Replace `CachedApi.IsColdCacheFetch` / `CachedApi.HasLoadError` usage with result record properties
    - _Requirements: 3.2, 3.3_

  - [x] 6.2 Write unit tests for DayPlanPage loading indicator behavior
    - Test that loading indicator activates for active date refresh task
    - Test that loading indicator does not activate for adjacent-day prefetches
    - _Requirements: 3.2_

- [x] 7. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck (minimum 100 iterations)
- Unit tests validate specific examples and edge cases using xUnit
- The loading indicator refactor moves indicator management from CachedApiClient to pages, so pages control when the spinner shows based on whether the fetch is for active content or a silent prefetch
- CalendarGrid already renders correctly with an empty `Days` list — no changes needed there

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "2.2"] },
    { "id": 2, "tasks": ["2.3", "3.1", "3.2", "3.3"] },
    { "id": 3, "tasks": ["5.1", "5.2", "6.1"] },
    { "id": 4, "tasks": ["5.3", "5.4", "6.2"] }
  ]
}
```
