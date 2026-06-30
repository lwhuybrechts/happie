# Implementation Plan: Offline Cache

## Overview

This plan implements a stale-while-revalidate caching layer and offline mutation queue for the Happie PWA. The implementation spans the Blazor WebAssembly client (IndexedDB via JS interop, CachedApiClient facade, SyncService, ConnectivityService, UI components) and minimal server-side changes (LastModified property + If-Unmodified-Since conflict detection). Tasks are ordered so foundational services are built first, then the facade that wires them together, then UI components, and finally server-side conflict detection.

## Tasks

- [x] 1. Set up JS interop module and client-side C# models
  - [x] 1.1 Create `wwwroot/js/cacheDb.js` IndexedDB module
    - Create the JS module with database `happie-cache`, version 1
    - Implement object stores: `dayPlanCache` (index: `byHousehold`), `calendarCache` (index: `byHousehold`), `mutationQueue` (index: `byHousehold`, auto-increment id)
    - Expose all methods defined in the design: `initialize`, `getDayPlan`, `putDayPlan`, `deleteDayPlan`, `getDayPlanCount`, `getOldestDayPlanKey`, `getCalendar`, `putCalendar`, `deleteCalendar`, `getCalendarKeys`, `enqueueMutation`, `dequeueMutation`, `peekAllMutations`, `clearAll`, `isAvailable`
    - Register the script in `index.html`
    - _Requirements: 4.5, 6.1, 6.8_

  - [x] 1.2 Create client-side C# record models
    - Create `CachedDayPlan`, `CachedCalendar`, `QueuedMutation` records in `Happie.Web/Services/Caching/`
    - _Requirements: 1.1, 2.1, 6.1_

  - [x] 1.3 Create `IConnectivityService` / `ConnectivityService`
    - Implement in `Happie.Web/Services/`
    - Wrap `navigator.onLine` and `online`/`offline` window events via JS interop
    - Expose `bool IsOnline` property and `OnConnectivityChanged` event
    - Register as Scoped in `Program.cs`
    - _Requirements: 5.4, 7.1, 7.2, 7.6_

  - [x] 1.4 Create `LoadingIndicatorState` service
    - Implement in `Happie.Web/Services/`
    - Track active operation count; expose `bool IsVisible` with 500ms minimum visibility
    - Expose `IncrementAsync()` and `DecrementAsync()` methods
    - Register as Scoped in `Program.cs`
    - _Requirements: 3.1, 3.2, 10.1, 10.2_

- [x] 2. Implement CacheStore and MutationQueue services
  - [x] 2.1 Create `ICacheStore` / `CacheStore`
    - Implement in `Happie.Web/Services/Caching/`
    - Wrap JS interop calls to `window.happieCache` for DayPlan and Calendar CRUD
    - Implement LRU eviction for DayPlan (max 30 entries per household)
    - Implement 2-entry limit for Calendar (current month preserved)
    - Handle IndexedDB unavailable gracefully (return nulls, no-op writes)
    - Register as Scoped in `Program.cs`
    - _Requirements: 1.1, 1.3, 1.4, 2.1, 2.3, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 5.1, 5.2_

  - [x] 2.2 Write property test: Cache round-trip preserves data
    - **Property 1: Cache round-trip preserves data**
    - **Validates: Requirements 1.1, 2.1, 5.1, 5.2**

  - [x] 2.3 Write property test: DayPlan LRU eviction at 30 entries
    - **Property 6: DayPlan cache enforces LRU eviction at 30 entries**
    - **Validates: Requirements 4.1, 4.2**

  - [x] 2.4 Write property test: Calendar 2-entry limit preserving current month
    - **Property 7: Calendar cache enforces 2-entry limit preserving current month**
    - **Validates: Requirements 4.3, 4.4**

  - [x] 2.5 Create `IMutationQueue` / `MutationQueue`
    - Implement in `Happie.Web/Services/Caching/`
    - Wrap JS interop calls for `enqueueMutation`, `dequeueMutation`, `peekAllMutations`
    - Store method, URL, headers (Authorization, X-Housemate-Id), body, createdAt, date, mutationType
    - Handle IndexedDB unavailable gracefully
    - Register as Scoped in `Program.cs`
    - _Requirements: 6.1, 6.8, 6.9_

  - [x] 2.6 Write property test: Mutation queue preserves data and FIFO order
    - **Property 9: Mutation queue preserves data and FIFO order**
    - **Validates: Requirements 6.1, 6.8, 6.9**

  - [x] 2.7 Write property test: Cache and queue isolated by household
    - **Property 17: Cache and queue isolated by household**
    - **Validates: Requirements 9.1, 9.3**

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement CachedApiClient facade
  - [x] 4.1 Create `ICachedApiClient` / `CachedApiClient`
    - Implement in `Happie.Web/Services/Caching/`
    - Inject `ICacheStore`, `IMutationQueue`, `IConnectivityService`, `LoadingIndicatorState`, `HttpClient`
    - Implement stale-while-revalidate for GET requests (DayPlan, Calendar)
    - Implement online write-through: send HTTP, update cache on success
    - Implement offline write: enqueue mutation, apply optimistic update to cache
    - Fire background refresh only when online; skip when offline
    - On background refresh success with different data: update cache, notify UI via callback
    - On background refresh 401: clear session, clear cache/queue, redirect to login with `forceLoad: true`
    - On cold cache fetch failure: surface error with retry option
    - Handle calendar in-place update on attendance change
    - Do not create cache entries for uncached dates on mutation success
    - Register as Scoped in `Program.cs`
    - _Requirements: 1.1–1.9, 2.1–2.6, 5.1–5.5, 6.1–6.2, 8.1–8.5, 9.1–9.4_

  - [x] 4.2 Write unit tests for CachedApiClient
    - Test cold cache path (no cache entry → fetch → store)
    - Test stale-while-revalidate path (cache hit → return → background refresh)
    - Test offline write (enqueue + optimistic update)
    - Test 401 handling (clear session, redirect)
    - Test IndexedDB unavailable graceful degradation
    - _Requirements: 1.1, 1.5, 1.7, 1.8, 1.9, 6.2, 8.5_

- [x] 5. Implement SyncService with retry and rollback
  - [x] 5.1 Create `ISyncService` / `SyncService`
    - Implement in `Happie.Web/Services/Caching/`
    - Subscribe to `IConnectivityService.OnConnectivityChanged`; start replay within 5 seconds of `online` event
    - Replay mutations sequentially in FIFO order via HttpClient
    - Add `If-Unmodified-Since` header from mutation's `createdAt` timestamp
    - On 2xx: remove mutation from queue
    - On 4xx / 409: discard mutation, roll back optimistic change in cache, show localized toast
    - On 5xx / network error: retry with exponential backoff (2s, 4s, 8s, 16s, 32s; max 5 attempts)
    - On exhausted retries: discard mutation, roll back, show localized toast
    - Increment/decrement `LoadingIndicatorState` during replay
    - Limit visible toasts to 3 simultaneously; queue additional
    - Auto-dismiss toasts after 8 seconds
    - Register as Scoped in `Program.cs`
    - _Requirements: 6.3–6.13, 10.1–10.4_

  - [x] 5.2 Write property test: Exponential backoff delay formula
    - **Property 12: Exponential backoff delay formula**
    - **Validates: Requirements 6.6**

  - [x] 5.3 Write property test: Replayed mutations include If-Unmodified-Since header
    - **Property 13: Replayed mutations include If-Unmodified-Since header**
    - **Validates: Requirements 6.10**

  - [x] 5.4 Write unit tests for SyncService
    - Test 4xx rollback flow
    - Test 409 conflict toast message
    - Test exhausted retries terminal state
    - Test successful replay removes mutation
    - _Requirements: 6.4, 6.5, 6.7, 6.13_

- [x] 6. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Implement UI components
  - [x] 7.1 Create `LoadingIndicator` component
    - Implement in `Happie.Web/Shared/`
    - 16px spinning animation positioned per design (mobile: right of "HAPPIE" text; desktop: right of sidebar brand)
    - CSS animation only (no JS thread blocking)
    - `prefers-reduced-motion: reduce` → pulsing opacity instead of spin
    - Include `aria-label` with localized description via `IStringLocalizer<AppStrings>`
    - Inject `LoadingIndicatorState`; show/hide based on `IsVisible`
    - Add localization keys to `AppStrings.resx` (Dutch) and `AppStrings.en.resx` (English)
    - _Requirements: 3.1–3.7_

  - [x] 7.2 Create `OfflineBanner` component
    - Implement in `Happie.Web/Shared/`
    - Fixed position, z-index 1050
    - Show within 1 second of connectivity loss; hide within 1 second of restoration
    - Show immediately on init if `navigator.onLine` is `false`
    - Display localized message via `IStringLocalizer<AppStrings>`
    - Do not display on login page
    - Add localization keys to `AppStrings.resx` and `AppStrings.en.resx`
    - _Requirements: 7.1–7.7_

  - [x] 7.3 Create `SyncToast` component
    - Implement in `Happie.Web/Shared/`
    - Toast notification for sync failures
    - Display mutation type and target date (localized)
    - Auto-dismiss after 8 seconds or on manual close
    - Maximum 3 visible simultaneously; queue additional
    - Add localization keys to `AppStrings.resx` and `AppStrings.en.resx`
    - _Requirements: 10.3, 10.4_

  - [x] 7.4 Write unit tests for OfflineBanner
    - Test not shown on login page
    - Test shown/hidden on connectivity change
    - _Requirements: 7.1, 7.2, 7.7_

- [x] 8. Integrate CachedApiClient into pages
  - [x] 8.1 Refactor `DayPlanPage` to use `ICachedApiClient`
    - Replace direct `HttpClient` data calls with `ICachedApiClient.GetDayPlanAsync(date)`
    - Replace mutation calls with `ICachedApiClient` write methods
    - Remove "Loading..." text display when cache entry exists; render cached data immediately
    - Handle cold cache path (show loading indicator, fetch, display)
    - Handle cold cache failure (show error with retry)
    - Handle no-cache-offline state (show localized message)
    - Support background refresh UI update callback
    - _Requirements: 1.1, 1.5, 1.6, 1.9, 5.1, 5.3, 5.5, 6.2_

  - [x] 8.2 Refactor `CalendarPage` to use `ICachedApiClient`
    - Replace direct `HttpClient` data calls with `ICachedApiClient.GetCalendarAsync(month)`
    - Handle stale-while-revalidate for calendar data
    - Handle no-cache-offline state
    - _Requirements: 2.1, 2.5, 5.2, 5.3_

  - [x] 8.3 Add `LoadingIndicator` and `OfflineBanner` to `MainLayout`
    - Include `LoadingIndicator` in header/sidebar per viewport
    - Include `OfflineBanner` above page content
    - _Requirements: 3.3, 3.4, 7.1_

- [x] 9. Implement session and household scoping
  - [x] 9.1 Implement cache/queue clearing on logout and 401
    - On logout or JWT expiry (401 response): clear all cache entries and mutation queue
    - _Requirements: 8.3_

  - [x] 9.2 Implement household switch detection
    - On login: compare new HouseholdId with previously stored HouseholdId
    - If different: clear all cache entries and mutation queue for previous household
    - Discard unsynced mutations for previous household without replay or error
    - _Requirements: 8.4, 9.1, 9.2, 9.3, 9.4_

- [x] 10. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. Server-side conflict detection
  - [x] 11.1 Add `LastModified` property to entities
    - Add `LastModified` (`DateTimeOffset`) to `AttendanceRecordEntity`, `DishRecordEntity`, `CommentEntity`
    - Update mappers to map `LastModified` to/from domain types
    - Update handlers to set `LastModified = DateTimeOffset.UtcNow` on every successful write
    - _Requirements: 6.11, 6.12_

  - [x] 11.2 Implement `If-Unmodified-Since` conflict check in mutation handlers
    - In `DayHandler` (attendance, dish, comment PUT/DELETE endpoints): read `If-Unmodified-Since` header
    - If present and entity's `LastModified` is strictly after the header value: return HTTP 409 with `ApiErrorResponse` and code `CONFLICT`
    - If not present, or `LastModified` is at or before header value, or entity does not exist: proceed normally
    - Add `CONFLICT` to `ApiErrorCodes`
    - _Requirements: 6.10, 6.11, 6.12_

  - [x] 11.3 Write property test: Server conflict detection
    - **Property 14: Server conflict detection**
    - **Validates: Requirements 6.11, 6.12**

  - [x] 11.4 Write unit tests for conflict detection
    - Test entity does not exist (proceed normally)
    - Test timestamps exactly equal (proceed normally)
    - Test entity modified after header value (return 409)
    - _Requirements: 6.11, 6.12_

- [x] 12. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck (minimum 100 iterations)
- Unit tests validate specific examples and edge cases using xUnit
- All localized strings use `IStringLocalizer<AppStrings>` with keys in both `AppStrings.resx` (Dutch) and `AppStrings.en.resx` (English)
- The JS interop module (`cacheDb.js`) is the single point of contact with IndexedDB; C# services never access IndexedDB directly

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4"] },
    { "id": 1, "tasks": ["2.1", "2.5"] },
    { "id": 2, "tasks": ["2.2", "2.3", "2.4", "2.6", "2.7"] },
    { "id": 3, "tasks": ["4.1"] },
    { "id": 4, "tasks": ["4.2", "5.1"] },
    { "id": 5, "tasks": ["5.2", "5.3", "5.4"] },
    { "id": 6, "tasks": ["7.1", "7.2", "7.3", "11.1"] },
    { "id": 7, "tasks": ["7.4", "8.1", "8.2", "8.3", "11.2"] },
    { "id": 8, "tasks": ["9.1", "9.2", "11.3", "11.4"] }
  ]
}
```
