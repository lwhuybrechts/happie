# Happie — Domain Rules & Constraints

## Multi-Household Data Scoping

- Every entity (Housemate, AttendanceRecord, DishRecord, Comment, DayHistory, PushSubscription) is scoped to a `HouseholdId` (Guid)
- All queries MUST filter by `HouseholdId`; never return data across household boundaries
- Household creation and password management are out of scope for the app UI — an administrator inserts records directly in the database
- The app MUST NOT expose any UI for creating, modifying, or deleting Households or Household_Passwords

## Authentication & Session

- Authentication is household-level: the user enters a shared household password, not a personal one
- A successful login returns a JWT scoped to the matched `HouseholdId`
- The `ActiveHousemateId` is stored separately from the JWT and sent as `X-Housemate-Id` on every request
- All write operations (attendance, dish, comment, nudge) are attributed to the `ActiveHousemateId`
- Every resulting `DayHistory` entry must record the acting housemate's ID

## Housemate Lifecycle

- Adding a housemate requires only a name (no password)
- Housemate names may be duplicated within a household; colors may not
- When removing a housemate:
  - No linked attendance records or comments → hard delete (removed from all listings)
  - At least one linked attendance record or comment → soft delete (`IsDeleted = true`)
- Soft-deleted housemates MUST NOT appear in the active housemate list or in new Day_Plans
- Wherever a soft-deleted housemate's historical data appears, their display name MUST be rendered as `"Name (deleted)"`

## Housemate Colors

- Colors are chosen from a predefined palette of exactly 30 hex values (defined in `HousemateColors.Palette`)
- All `Housemate_Color` values within a household MUST be unique among active housemates
- When adding a new housemate, auto-assign the first palette color not already in use
- Attempting to assign a color already in use MUST be rejected with HTTP 409 `COLOR_CONFLICT`
- Housemate color is used as the visual identity of a housemate throughout the entire app

## Attendance

- Three valid statuses: `Unknown`, `EatingIn`, `NotEatingIn`
- Any housemate may change the attendance status of any other housemate for any day
- Setting attendance is always an overwrite (last write wins)
- The day plan response MUST include an attendance entry for every active (non-deleted) housemate

## Dish

- One dish per day per household (not per housemate)
- Setting a dish is always an overwrite; the last writer wins
- Max 100 characters, trimmed — enforced on both client and server
- Saving is attributed to the active housemate in `DishRecords.LastChangedByHousemateId`

## Comments

- Exactly one comment slot per housemate per day (upsert semantics on PUT)
- Max 200 characters, trimmed — enforced on both client and server
- DELETE removes the slot entirely; there is no "empty comment" state

## Nudges (manual push reminders)

- The NudgeModal shows all housemates in the household (excluding the active housemate) as recipient chips
- Housemates whose `Attendance_Status` is `Unknown` are pre-selected by default; others are shown but not selected
- The sender can select/deselect any recipient before sending
- The notification payload MUST include the sender's name and the target date
- `predefinedMessageKey` and `message` are mutually exclusive; exactly one must be set
- Custom message max 20 characters, trimmed
- Predefined message keys are resolved server-side in the recipient's stored locale
- Push delivery failure for one recipient MUST NOT prevent delivery to others; failures are reported back to the sender

## Automatic Push Notifications (on Day_Plan changes)

- Triggered automatically when any Day_Plan field (attendance, dish, comment) changes for today or tomorrow
- Recipients: all active housemates in the household EXCEPT the one who made the change
- Payload MUST include: actor name, affected date, description of what changed
- Push delivery failure MUST be logged server-side but MUST NOT interrupt or roll back the save operation

## Push Subscription Management

- Subscriptions are registered via `POST /api/push/subscribe` after the user grants browser permission
- Each housemate has at most one subscription record (upsert semantics)
- The subscription record stores the housemate's locale for server-side message rendering
- When the browser issues a new subscription (key rotation), the client MUST re-register it

## Offline Behavior

- The Service Worker caches the most recently loaded Day_Plans for offline access
- While offline, the UI MUST show the `OfflineBanner` to indicate data may be stale
- Mutations made offline are queued locally and replayed against the backend when connectivity is restored
- Failed sync items are retried with exponential backoff; persistent failures are surfaced to the user

## Optimistic UI & Error Handling

- Attendance, dish, and comment saves use optimistic UI: apply the change immediately, roll back to the previous value if the API call fails
- All API failures surface a toast notification with the error message
- Validation is enforced on both client and server for all field length rules

## Validation Rules

| Field | Rule |
|---|---|
| Dish | Max 100 characters, trimmed |
| Comment | Max 200 characters, trimmed |
| Nudge custom message | Max 20 characters, trimmed |
| Housemate name | 1–50 characters, trimmed, not empty |
| Housemate color | Must be a value from `HousemateColors.Palette` |

## Calendar View

- The CalendarPage is read-only; attendance can only be changed from the DayPlanPage
- Each calendar cell shows the `Housemate_Color` dots of housemates with `EatingIn` status on that day
- A day with no `EatingIn` housemates shows no color indicators
- Tapping a day in the CalendarPage navigates to the DayPlanPage for that day

## What is Explicitly Out of Scope

- No UI for household creation, deletion, or password management
- No per-housemate passwords or individual account management
- No multi-region or high-availability infrastructure (Azure Table Storage free tier is sufficient)
