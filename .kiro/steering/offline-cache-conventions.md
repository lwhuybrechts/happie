---
inclusion: fileMatch
fileMatchPattern: "**/Caching/**,**/cacheDb*,**/SyncService*,**/MutationQueue*,**/ConnectivityService*,**/CachedApiClient*"
---

# Happie — Offline Cache & Sync Conventions

## Architecture

The app uses application-level caching (IndexedDB via JS interop) for offline support. This is separate from the Service Worker's static asset caching.

### Key Services (all in `Happie.Web/Services/Caching/`)

| Service | Responsibility |
|---|---|
| `ICachedApiClient` / `CachedApiClient` | Central API facade: stale-while-revalidate for GETs, offline queueing for writes |
| `ICacheStore` / `CacheStore` | IndexedDB CRUD for DayPlan, Calendar, and SavedDish cache entries |
| `IMutationQueue` / `MutationQueue` | IndexedDB queue for offline write operations |
| `ISyncService` / `SyncService` | Replays queued mutations on reconnect with exponential backoff |
| `IConnectivityService` / `ConnectivityService` | Tracks online/offline state via `navigator.onLine` events |
| `LoadingIndicatorState` | Tracks active background operations for the spinner |

### JS Interop Module

<!-- MAINTENANCE: when adding a new object store, add it to this list and update the DB version. -->

`wwwroot/js/cacheDb.js` — manages the `happie-cache` IndexedDB database (version 2) with four object stores:
- `dayPlanCache` — cached DayPlan responses (max 30 per household, LRU eviction)
- `calendarCache` — cached Calendar responses (cluster-based eviction around today and viewed month)
- `savedDishCache` — cached saved dishes list (1 entry per household, refetch-and-replace on mutation)
- `mutationQueue` — offline mutations awaiting replay (FIFO)

### Initialization

All caching services MUST be initialized in `Program.cs` after the host is built:

```csharp
var cacheStore = host.Services.GetRequiredService<ICacheStore>();
await cacheStore.InitializeAsync();
var mutationQueue = host.Services.GetRequiredService<IMutationQueue>();
await mutationQueue.InitializeAsync();
var connectivityService = host.Services.GetRequiredService<IConnectivityService>();
await connectivityService.InitializeAsync();
var syncService = host.Services.GetRequiredService<ISyncService>();
await syncService.InitializeAsync();
```

Without these calls, IndexedDB is never opened and all cache operations silently no-op.

### Session handling

- `AuthHeaderHandler` only injects headers — it does NOT handle 401 responses or redirect.
- 401 handling is done by `CachedApiClient` (clears session + cache + queue, saves returnUrl, redirects to login).
- When `householdId` is missing from localStorage (no session), `CachedApiClient` GET methods redirect to login automatically.
- `SyncService` treats 401 during replay as a 4xx (discard mutation + rollback).

### Server-side conflict detection

Mutation endpoints support `If-Unmodified-Since` header. When present and the entity's `LastModified` is strictly after the header value, the server returns HTTP 409 (`CONFLICT`). The `SyncService` adds this header from the mutation's `createdAt` timestamp during replay.

---

## Saved Dishes Cache

The saved dishes cache stores the household's full saved dish list as a single entry per household. It uses stale-while-revalidate for reads but does NOT queue mutations offline (CRUD operations require connectivity).

### Key behaviors

- **Single entry per household** — unlike DayPlan (up to 30) or Calendar (cluster-based), saved dishes has exactly 1 entry keyed by `householdId`
- **Refetch-and-replace on mutation** — after a successful create/rename/delete, `RefreshSavedDishesCacheAsync()` refetches from the API and replaces the cache entry
- **Pre-populated on first DayPlan load** — the cache is populated eagerly during the first `GetDayPlanAsync` call to ensure the SavedDishModal is instant on first use
- **No offline mutation queueing** — saved dish CRUD is complex (duplicate detection, soft-delete reactivation) and requires connectivity, consistent with housemate management
- **Background refresh does not interrupt open modal** — fresh data is stored for the next open

### CachedApiClient methods for saved dishes

| Method | Behavior |
|---|---|
| `GetSavedDishesAsync()` | Stale-while-revalidate; returns `SavedDishesFetchResult` |
| `RefreshSavedDishesCacheAsync()` | Refetches from API and replaces cache (call after mutations) |
| `OnSavedDishesUpdated` event | Notifies subscribers of background refresh changes |

---

## Client-Side API Calls — CachedApiClient (MUST follow)

All day-plan, calendar, and saved dishes read operations in **pages and components** MUST go through `ICachedApiClient` — NEVER use `HttpClient` directly for these operations.

`ICachedApiClient` provides:
- **Stale-while-revalidate** for GET requests (DayPlan, Calendar, SavedDishes)
- **Offline mutation queueing** with optimistic UI for writes
- **Cache updates** on successful mutations
- **Conflict detection** via `If-Unmodified-Since` on replayed mutations

### When to use `ICachedApiClient`

<!-- MAINTENANCE: when adding a new cached endpoint, add it to this table. -->

| Operation | Use `ICachedApiClient` |
|---|---|
| GET day plan | `GetDayPlanAsync(date)` |
| GET calendar | `GetCalendarAsync(viewedMonth)` |
| GET saved dishes | `GetSavedDishesAsync()` |
| PUT attendance | `SaveAttendanceAsync(date, housemateId, status)` |
| PUT dish | `SaveDishAsync(date, description, hour, minute, tzOffset)` |
| DELETE dish | `DeleteDishAsync(date)` |
| PUT saved dish links | `SaveDishLinksAsync(date, savedDishIds)` |
| PUT chef status | `SaveChefStatusAsync(date, housemateId, isChef)` |
| PUT comment | `SaveCommentAsync(date, housemateId, text)` |
| DELETE comment | `DeleteCommentAsync(date, housemateId)` |

Any page or component that reads or writes day plan, calendar, or saved dishes data MUST use the methods above. This includes `DayPlanPage`, `CalendarPage`, `SavedDishesPage`, and all child components (`AttendanceSection`, `AttendanceToggle`, `DishPanel`, `SavedDishModal`, `CommentsSection`, `CommentEditor`).

### When to use `HttpClient` directly

- Login (`POST /api/auth/login`) — not cacheable, not day-plan/calendar scoped
- Housemate management (`GET/POST/PATCH/DELETE /api/housemates`) — not day-plan/calendar scoped
- Saved dish CRUD (`POST/PATCH/DELETE /api/saved-dishes`) — requires connectivity, complex server logic
- Push subscribe (`POST /api/push/subscribe`) — not cacheable
- Nudge (`POST /api/days/{date}/nudge`) — requires connectivity, not queueable

### Injection pattern

```csharp
// ✅ GOOD: inject ICachedApiClient for day-plan mutations.
@using Happie.Web.Services.Caching
@inject ICachedApiClient CachedApi

var success = await CachedApi.SaveAttendanceAsync(Date, housemateId, newStatus);
```

```csharp
// ❌ BAD: direct HttpClient for day-plan operations — crashes offline.
@inject HttpClient Http

var response = await Http.PutAsJsonAsync($"days/{Date}/attendance/{housemateId}", request);
```

### Rules

- Do NOT inject `HttpClient` in a component if all its HTTP calls go through `ICachedApiClient`
- Components that need both (e.g., nudge + attendance, or saved dish CRUD + saved dish reads) may inject both, but cached operations MUST use `ICachedApiClient`
- `ICachedApiClient` methods return `bool` for success/failure — use this for optimistic rollback instead of checking `HttpResponseMessage.IsSuccessStatusCode`

---

## Extending the Offline Cache (MUST follow)

### Adding a new API endpoint to the cache

To add stale-while-revalidate caching for a new GET endpoint:

1. Add a JS method to `cacheDb.js` for the new object store (if the data type doesn't fit an existing store). Bump the DB version.
2. Add `Get{Type}Async` and `Put{Type}Async` methods to `ICacheStore` / `CacheStore`.
3. Add a `Get{Type}Async` method to `ICachedApiClient` / `CachedApiClient` that follows the stale-while-revalidate pattern:
   - Check cache → return cached + fire background refresh → update cache if data changed → notify UI via event.
4. Add an `On{Type}Updated` event to `ICachedApiClient` for background refresh notifications.
5. Subscribe to the event in the consuming page/component.
6. Update this document: add the new store to the JS Interop Module list and the new methods to the CachedApiClient table.

### Adding a new mutation type to the offline queue

To add offline support for a new write endpoint:

1. Add a `Save{Type}Async` or `Delete{Type}Async` method to `ICachedApiClient` / `CachedApiClient` that:
   - Online: sends HTTP, updates relevant cache entry on success.
   - Offline: enqueues the mutation (with method, URL, headers, body, date, mutationType), applies optimistic update to cache.
2. Add the optimistic update logic (`Apply{Type}OptimisticUpdate`) as a private method in `CachedApiClient`.
3. Add a rollback case for the new `mutationType` in `SyncService.RollbackMutation`.
4. Add the localized mutation type name to `SyncService.GetLocalizedMutationType` and the corresponding `Sync_MutationType_{Type}` resource keys.
5. If conflict detection is needed: add `LastModified` to the server entity and check `If-Unmodified-Since` in the handler.

### Pages and components that use `HttpClient` directly (not cached)

These operations inherently require connectivity and are NOT cached/queued:
- Login, Housemate management, Saved dish CRUD, Push subscribe, Nudge

They MUST include:
- A connectivity check (`ConnectivityService.IsOnline`) before the HTTP call.
- A try/catch around the HTTP call.
- A localized "requires internet" error message when offline (`Error_RequiresInternet` key).
