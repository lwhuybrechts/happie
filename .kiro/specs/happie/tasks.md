# Implementation Plan: Happie

## Overview

Incremental implementation of the Happie PWA: backend Azure Functions + Table Storage first, then Blazor WASM frontend, then push notifications, then offline/PWA support. Each task wires into the previous one so there is no orphaned code.

## Tasks

- [x] 1. Project scaffolding and shared types
  - Create the Azure Functions isolated-worker project (`.NET 10`) and the Blazor WASM project (`.NET 10`) under a single solution
  - Add shared class library for domain types: `AttendanceStatus`, `Housemate`, `Household`, `AttendanceRecord`, `DishRecord`, `Comment`, `NudgeRequest`, `NudgeMessageKey`, `PushSubscription`, `DayHistoryEntry`, `HousemateColors.Palette`
  - Add xUnit + FsCheck NuGet packages to the test project; configure FsCheck minimum 100 iterations
  - Add `Azure.Data.Tables`, `Azure.Security.KeyVault.Secrets`, and `Azure.Identity` NuGet packages to the Functions project
  - Add `Sentry.Extensions.Logging` NuGet package to the Functions project for Sentry integration
  - _Requirements: 2.2_

- [x] 2. Azure Key Vault — secrets management
  - Provision an Azure Key Vault and store the following secrets: `JwtSigningKey`, `TableStorageConnectionString`, `VapidPublicKey`, `VapidPrivateKey`, `SentryDsn`
  - Configure the Azure Functions app to use `DefaultAzureCredential` (Managed Identity in Azure, local dev via `az login`) to access Key Vault at startup via `Azure.Extensions.AspNetCore.Configuration.Secrets`
  - Add a `local.settings.json` entry pointing to the Key Vault URI for local development; never commit actual secret values
  - Write a startup check that fails fast with a clear error if any required secret is missing
  - Create `SentryOptions` class in `Happie.Api/Options/` following the Options pattern; bind `SentryDsn` from configuration with `[Required]` validation
  - Register Sentry as an `ILogger` provider in the Functions startup using the DSN from `SentryOptions`; all `ILogger.Log*` calls and unhandled exceptions will flow to Sentry automatically
  - _Requirements: 2.2_

- [x] 3. Azure Table Storage infrastructure
  - [x] 3.1 Implement `TableStorageClient` wrapper
    - Create a typed wrapper around `TableServiceClient` that exposes helpers for upsert, get, delete, and prefix-range queries
    - Wire connection string from Key Vault secret `TableStorageConnectionString`
    - _Requirements: 2.2_

  - [x] 3.2 Implement `BaseRepository<TEntity>` and concrete repositories
    - Create abstract class `BaseRepository<TEntity>` in `Happie.Api/Repositories/` where `TEntity : MyTableEntity`
    - Constructor takes `ITableStorageClient` and binds a `private const string TableName` defined by each subclass
    - Expose `protected` async methods: `UpsertAsync`, `GetAsync`, `DeleteAsync`, `QueryByPartitionAsync`, `QueryByRowKeyPrefixAsync` — all delegating to `ITableStorageClient` with the bound table name
    - Create concrete repositories with their interfaces in `Happie.Api/Repositories/`:
      - `IHouseholdRepository` / `HouseholdRepository` — table `Households`
      - `IHousemateRepository` / `HousemateRepository` — table `Housemates`
      - `IAttendanceRepository` / `AttendanceRepository` — table `AttendanceRecords`
      - `IDishRepository` / `DishRepository` — table `DishRecords`
      - `ICommentRepository` / `CommentRepository` — table `Comments`
      - `IDayHistoryRepository` / `DayHistoryRepository` — table `DayHistory`
      - `IPushSubscriptionRepository` / `PushSubscriptionRepository` — table `PushSubscriptions`
    - Register all repositories in `Program.cs` as singletons
    - _Requirements: 2.2_

  - [x]* 3.3 Write property test for data isolation between households
    - **Property 6: Data isolation between households**
    - **Validates: Requirements 1.8, 2.2, 2.3**

- [x] 4. Authentication — backend
  - [x] 4.1 Implement `POST /api/auth/login`
    - Look up household via `IHouseholdRepository`; verify password hash (bcrypt); return signed JWT (signed with `JwtSigningKey` from Key Vault) scoped to `HouseholdId` + list of active housemates fetched via `IHousemateRepository`
    - Return 401 with `UNAUTHORIZED` code on mismatch
    - _Requirements: 1.1, 1.2, 1.6_

  - [x]* 4.2 Write property test: correct password returns correct household
    - **Property 1: Correct password returns correct household**
    - **Validates: Requirements 1.2**

  - [x]* 4.3 Write property test: wrong password is denied
    - **Property 4: Wrong password is denied**
    - **Validates: Requirements 1.6**

  - [x] 4.4 Implement JWT validation middleware / `IFunctionMiddleware`
    - Validate `Authorization: Bearer` JWT and `X-Housemate-Id` header on every protected function
    - Reject with 401 `UNAUTHORIZED` or 403 `FORBIDDEN` as appropriate
    - _Requirements: 1.7, 1.8_

  - [x]* 4.5 Write property test: logout invalidates session
    - **Property 5: Logout invalidates session**
    - **Validates: Requirements 1.7**

  - [x] 4.6 Write unit tests for authentication
    - Login with correct password returns expected household
    - Login with incorrect password returns 401
    - Logout clears session token
    - _Requirements: 1.1, 1.2, 1.6, 1.7_

- [x] 5. Housemate management — backend
  - [x] 5.1 Implement `GET /api/housemates`
    - Return all active (non-deleted) housemates for the household via `IHousemateRepository`
    - _Requirements: 12.1, 12.8_

  - [x]* 5.2 Write property test: active housemate list contains no deleted housemates
    - **Property 22: Active housemate list contains no deleted housemates**
    - **Validates: Requirements 12.1, 12.8**

  - [x] 5.3 Implement `POST /api/housemates`
    - Validate name (1–50 chars, trimmed, not empty); fetch existing housemates via `IHousemateRepository` to auto-assign first unused palette color; persist via `IHousemateRepository`
    - Return 422 `VALIDATION_ERROR` on invalid name
    - _Requirements: 12.3, 12.4, 12.10_

  - [x]* 5.4 Write property test: add housemate round-trip
    - **Property 23: Add housemate round-trip**
    - **Validates: Requirements 12.3**

  - [x]* 5.5 Write property test: housemate name validation
    - **Property 30: Housemate name validation**
    - **Validates: Requirements 12.4**

  - [x] 5.6 Implement `PATCH /api/housemates/{housemateId}` (rename + color change)
    - Validate name rules; check color uniqueness via `IHousemateRepository`; reject color already in use with 409 `COLOR_CONFLICT`; persist via `IHousemateRepository`
    - _Requirements: 12.11, 12.12, 12.13, 12.14_

  - [x]* 5.7 Write property test: color uniqueness invariant within a household
    - **Property 27: Color uniqueness invariant within a household**
    - **Validates: Requirements 12.10, 12.11, 12.12, 12.13**

  - [x]* 5.8 Write property test: rename round-trip
    - **Property 28: Rename round-trip**
    - **Validates: Requirements 12.14**

  - [x] 5.9 Implement `DELETE /api/housemates/{housemateId}`
    - Check for linked records via `IAttendanceRepository` and `ICommentRepository`; hard delete via `IHousemateRepository` if none, soft delete (`IsDeleted = true`) via `IHousemateRepository` otherwise
    - _Requirements: 12.5, 12.6, 12.7_

  - [x]* 5.10 Write property test: hard delete removes housemate with no history
    - **Property 24: Hard delete removes housemate with no history**
    - **Validates: Requirements 12.5**

  - [x]* 5.11 Write property test: soft delete preserves history but removes from active list
    - **Property 25: Soft delete preserves history but removes from active list**
    - **Validates: Requirements 12.6**

  - [x]* 5.12 Write property test: deleted housemate name formatted as "Name (deleted)"
    - **Property 26: Deleted housemate name formatted as "Name (deleted)"**
    - **Validates: Requirements 12.7**

  - [x] 5.13 Write unit tests for housemate management
    - Soft delete vs hard delete decision logic
    - Deleted housemate name formatting ("Name (deleted)")
    - Color conflict returns 409
    - Housemate name validation rejects empty, whitespace-only, and strings > 50 chars (boundary: 50, 51)
    - _Requirements: 12.4, 12.5, 12.6, 12.7, 12.12_

- [x] 6. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Day plan — backend
  - [x] 7.1 Implement `GET /api/days/{date}`
    - Fetch active housemates via `IHousemateRepository` (default `Unknown` attendance if no record), attendance via `IAttendanceRepository`, dish via `IDishRepository`, comments via `ICommentRepository`, and history entries via `IDayHistoryRepository`
    - Format soft-deleted housemate names as `"Name (deleted)"` in historical data
    - _Requirements: 3.4, 3.5, 3.6, 12.7_

  - [x]* 7.2 Write property test: day plan contains all active housemates' attendance
    - **Property 7: Day plan contains all active housemates' attendance**
    - **Validates: Requirements 3.4**

  - [x] 7.3 Implement `PUT /api/days/{date}/attendance/{housemateId}`
    - Upsert via `IAttendanceRepository`; write history entry via `IDayHistoryRepository` attributing the change to `X-Housemate-Id`
    - _Requirements: 4.1, 4.3, 4.4, 1.5_

  - [x]* 7.4 Write property test: attendance round-trip with overwrite semantics
    - **Property 8: Attendance round-trip with overwrite semantics**
    - **Validates: Requirements 4.1, 4.3, 4.4**

  - [x]* 7.5 Write property test: all actions attributed to the active housemate
    - **Property 3: All actions attributed to the active housemate**
    - **Validates: Requirements 1.5**

  - [x] 7.6 Implement `PUT /api/days/{date}/dish`
    - Validate max 100 chars (trimmed); upsert via `IDishRepository`; write history entry via `IDayHistoryRepository`
    - Return 422 `VALIDATION_ERROR` on length violation
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x]* 7.7 Write property test: dish round-trip with overwrite semantics
    - **Property 9: Dish round-trip with overwrite semantics**
    - **Validates: Requirements 5.1, 5.2, 5.3**

  - [x]* 7.8 Write property test: dish length validation
    - **Property 10: Dish length validation**
    - **Validates: Requirements 5.4**

  - [x] 7.9 Implement `PUT /api/days/{date}/comments/{housemateId}` and `DELETE`
    - PUT: validate max 200 chars (trimmed); upsert via `ICommentRepository`; write history entry via `IDayHistoryRepository`
    - DELETE: remove comment via `ICommentRepository`; write history entry via `IDayHistoryRepository`
    - Return 422 on length violation
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

  - [x]* 7.10 Write property test: comment slot — one per housemate per day
    - **Property 11: Comment slot — one per housemate per day**
    - **Validates: Requirements 6.1, 6.2, 6.3**

  - [x]* 7.11 Write property test: comment deletion removes the comment
    - **Property 12: Comment deletion removes the comment**
    - **Validates: Requirements 6.4**

  - [x]* 7.12 Write property test: comment length validation
    - **Property 13: Comment length validation**
    - **Validates: Requirements 6.5**

  - [x] 7.13 Implement `GET /api/days?from={date}&to={date}`
    - Fetch attendance summaries (housemate color + status) for the date range via `IAttendanceRepository` and `IHousemateRepository`; used by CalendarPage
    - _Requirements: 13.1, 13.2, 13.4_

  - [x]* 7.14 Write property test: calendar color indicators match eating-in housemates
    - **Property 29: Calendar color indicators match eating-in housemates**
    - **Validates: Requirements 13.2, 13.4**

  - [x] 7.15 Write unit tests for day plan endpoints
    - Dish validation rejects strings > 100 chars (boundary: 100, 101)
    - Comment validation rejects strings > 200 chars (boundary: 200, 201)
    - DayHistoryLog entries are ordered reverse-chronologically
    - _Requirements: 5.4, 6.5_

- [x] 8. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Push notifications — backend
  - [x] 9.1 Implement `POST /api/push/subscribe`
    - Upsert push subscription for the active housemate via `IPushSubscriptionRepository`; store locale from request body
    - _Requirements: 8.2, 8.4_

  - [x] 9.2 Implement `POST /api/days/{date}/nudge`
    - Validate recipients are all `Unknown` status via `IAttendanceRepository`; validate `predefinedMessageKey` XOR `message` (max 20 chars, trimmed)
    - Fetch recipient subscriptions via `IPushSubscriptionRepository`; resolve predefined message keys in each recipient's stored locale; dispatch VAPID push per recipient using `VapidPublicKey` and `VapidPrivateKey` from Key Vault
    - Collect per-recipient failures; log each failure via `ILogger.LogWarning` (flows to Sentry automatically) and return them in the response without aborting delivery to others
    - _Requirements: 7.1, 7.2, 7.4, 7.5_

  - [x]* 9.3 Write property test: nudge payload contains sender and date
    - **Property 14: Nudge payload contains sender and date**
    - **Validates: Requirements 7.2**

  - [x]* 9.4 Write property test: nudge default recipients are housemates with unknown status
    - **Property 15: Nudge default recipients are housemates with unknown status**
    - **Validates: Requirements 7.4**

  - [x] 9.5 Implement automatic push notifications on day plan changes
    - After any successful attendance/dish/comment save for today or tomorrow, fetch all active housemate subscriptions via `IPushSubscriptionRepository` and dispatch push to all except the actor
    - Log push failures server-side via `ILogger.LogWarning` / `ILogger.LogError` (flows to Sentry automatically); do not roll back the save
    - _Requirements: 10.1, 10.2, 10.3, 10.5_

  - [x]* 9.6 Write property test: auto-notification recipients exclude the sender
    - **Property 17: Auto-notification recipients exclude the sender**
    - **Validates: Requirements 10.1, 10.3**

  - [x]* 9.7 Write property test: auto-notification payload contains actor, date, and change description
    - **Property 18: Auto-notification payload contains actor, date, and change description**
    - **Validates: Requirements 10.2**

  - [x]* 9.8 Write property test: push failure does not interrupt save
    - **Property 19: Push failure does not interrupt save**
    - **Validates: Requirements 10.5**

  - [x] 9.9 Write unit tests for push notifications
    - Nudge message validation rejects strings > 20 chars (boundary: 20, 21)
    - Auto-notification is not sent to the housemate who made the change
    - Push failure does not cause save to fail (mock push service throws, save succeeds)
    - _Requirements: 7.5, 10.3, 10.5_

- [x] 10. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. Blazor WASM — project setup and i18n
  - [x] 11.1 Configure Blazor WASM PWA project
    - Set up routing (`/`, `/day/{date}`, `/calendar`, `/housemates`)
    - Add `HttpClient` with base address pointing to `/api`; configure JWT + `X-Housemate-Id` header injection via `DelegatingHandler`
    - _Requirements: 1.1, 3.1_

  - [x] 11.2 Implement i18n resource files and locale switching
    - Add `en` and `nl` `.resx` resource files for all UI strings
    - Implement locale persistence in `localStorage`; default to `"nl"` when unset
    - Wire locale switcher so language changes immediately without page reload
    - _Requirements: 11.1, 11.2, 11.3, 11.4_

  - [x]* 11.3 Write property test: all translation keys exist in both locales
    - **Property 20: All translation keys exist in both locales**
    - **Validates: Requirements 11.1**

  - [x]* 11.4 Write property test: locale persistence round-trip
    - **Property 21: Locale persistence round-trip**
    - **Validates: Requirements 11.3**

  - [x] 11.5 Write unit test: default locale is "nl" when no locale is set
    - _Requirements: 11.4_

- [x] 12. Blazor WASM — LoginPage and session management
  - [x] 12.1 Implement `LoginPage` (`/`)
    - Password entry form; on success store JWT in `localStorage`, display housemate selection list
    - On housemate selection store `ActiveHousemateId` in `localStorage`; redirect to `/day/{today}`
    - Show error message on wrong password
    - _Requirements: 1.1, 1.2, 1.3, 1.6_

  - [x]* 12.2 Write property test: active housemate selection round-trip
    - **Property 2: Active housemate selection round-trip**
    - **Validates: Requirements 1.3, 1.4**

  - [x] 12.3 Implement session restore on app startup
    - On load, read JWT + `ActiveHousemateId` from `localStorage`; if valid JWT skip login screen
    - _Requirements: 1.4, 1.5_

  - [x] 12.4 Implement logout
    - Clear JWT and `ActiveHousemateId` from `localStorage`; redirect to `/`
    - _Requirements: 1.7_

- [x] 13. Blazor WASM — DayPlanPage and components
  - [x] 13.1 Implement `AttendanceToggle` component
    - Three-state toggle (EatingIn / NotEatingIn / Unknown) styled with the housemate's color
    - Optimistic UI: apply change immediately, roll back on API failure; show toast on error
    - Call `PUT /api/days/{date}/attendance/{housemateId}`
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

  - [x] 13.2 Implement `DishEditor` component
    - Inline editable field, max 100 chars (client-side validation); optimistic save with rollback
    - Call `PUT /api/days/{date}/dish`; show toast on error
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

  - [x] 13.3 Implement `CommentEditor` component
    - Inline editable field per housemate, max 200 chars (client-side validation); upsert on save, DELETE on clear
    - Optimistic save with rollback; show toast on error
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6_

  - [x] 13.4 Implement `DayHistoryLog` component
    - Fetch and display history entries for the day in reverse-chronological order
    - _Requirements: 3.6_

  - [x] 13.5 Implement `NudgeDialog` component
    - Modal pre-populated with housemates whose status is `Unknown`; allow deselection
    - `predefinedMessageKey` / custom message (max 20 chars) toggle; call `POST /api/days/{date}/nudge`
    - Show per-recipient failure list on partial failure
    - _Requirements: 7.1, 7.2, 7.4, 7.5_

  - [x] 13.6 Implement `DayPlanPage` (`/day/{date}`)
    - Compose `AttendanceToggle`, `DishEditor`, `CommentEditor`, `DayHistoryLog`, `NudgeDialog`
    - Swipe-left / swipe-right navigation to next/previous day
    - Default to today on initial load
    - _Requirements: 3.1, 3.2, 3.3, 3.7, 3.8_

- [x] 14. Blazor WASM — CalendarPage
  - [x] 14.1 Implement `CalendarGrid` component
    - Month grid; each cell shows color dots for housemates with `EatingIn` status; empty cell if none
    - Tap a day to navigate to `/day/{date}`
    - _Requirements: 13.1, 13.2, 13.3, 13.4_

  - [x] 14.2 Implement `CalendarPage` (`/calendar`)
    - Load attendance summaries via `GET /api/days?from=&to=`; render `CalendarGrid`
    - CalendarPage is read-only; tapping a day navigates to `/day/{date}`
    - _Requirements: 13.1, 13.2, 13.3, 13.4_

- [~] 15. Blazor WASM — HousematesPage
  - [ ] 15.1 Implement `HousemateColorPicker` component
    - Display the 30-color predefined palette; highlight current color; disable colors in use by other housemates
    - _Requirements: 12.11, 12.12_

  - [~] 15.2 Implement `HousematesPage` (`/housemates`)
    - List active housemates; add / rename / remove / color-change actions
    - Active housemate switch without re-entering password
    - Show error toast on any save failure; leave list unchanged on failure
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5, 12.6, 12.9, 12.10, 12.11, 12.12, 12.13, 12.14, 12.15_

- [ ] 16. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 17. Push notification permission and subscription — frontend
  - [~] 17.1 Implement push permission request flow
    - On first PWA launch, request notification permission; on grant call `POST /api/push/subscribe` with VAPID public key, subscription object, and current locale
    - Show informational message if permission is denied
    - _Requirements: 8.1, 8.2, 8.3_

  - [ ] 17.2 Implement subscription renewal
    - Listen for `pushsubscriptionchange` event in the Service Worker; re-register updated subscription via `POST /api/push/subscribe`
    - _Requirements: 8.4_

  - [ ] 17.3 Implement Service Worker push event handler
    - Handle incoming push events; show notification with title, body, and `data.url` for the relevant day
    - On notification click, open `/day/{date}`
    - _Requirements: 7.3, 10.4_

- [ ] 18. Offline support — Service Worker and sync queue
  - [ ] 18.1 Implement offline caching in Service Worker
    - Cache most recently loaded day plan responses; serve from cache when offline
    - _Requirements: 9.1_

  - [ ] 18.2 Implement `OfflineBanner` component
    - Show banner when `navigator.onLine` is false; hide when connectivity is restored
    - _Requirements: 9.2_

  - [ ] 18.3 Implement offline mutation queue
    - Queue attendance/dish/comment mutations in IndexedDB when offline
    - On connectivity restore, replay queued mutations against the backend with exponential backoff
    - Surface persistent failures to the user
    - _Requirements: 9.3, 9.4_

  - [ ]* 18.4 Write property test: offline mutations are applied after sync
    - **Property 16: Offline mutations are applied after sync**
    - **Validates: Requirements 9.3, 9.4**

- [ ] 19. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for a faster MVP
- Each task references specific requirements for traceability
- Property tests must be tagged: `// Feature: happie, Property {N}: {property_text}` and run a minimum of 100 iterations
- All 30 correctness properties from the design document are covered by property test sub-tasks
- Optimistic UI with rollback applies to attendance, dish, and comment saves throughout
- All secrets (JWT signing key, Table Storage connection string, VAPID keys, Sentry DSN) are stored in Azure Key Vault and accessed via `DefaultAzureCredential`; never commit secret values to source control
- Sentry is integrated as an `ILogger` provider via `Sentry.Extensions.Logging`; all `ILogger.Log*` calls and unhandled exceptions flow to Sentry automatically — no Sentry-specific API calls are needed in application code
- `SentryOptions` in `Happie.Api/Options/` binds the `SentryDsn` secret from Key Vault following the Options pattern
