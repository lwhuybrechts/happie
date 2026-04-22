# Happie — Project Context

## What is Happie

Happie is a Progressive Web App (PWA) for households to coordinate dinner attendance. Housemates indicate per day whether they are eating in, fill in the planned dish, add comments, and send push notification reminders (Nudges) to each other.

## Tech Stack

- **Frontend**: Blazor WebAssembly PWA (.NET 10), hosted on Azure Static Web Apps
- **Backend**: Azure Functions (isolated worker, .NET 10)
- **Database**: Azure Table Storage
- **Secrets**: Azure Key Vault — accessed via `DefaultAzureCredential` (Managed Identity in Azure, `az login` for local dev)
- **Push notifications**: VAPID Web Push protocol
- **Testing**: xUnit (unit tests), FsCheck (property-based tests)
- **Languages**: C# throughout; all source code, identifiers, and comments are in English

## Secrets Management

All secrets are stored in Azure Key Vault. The Functions app loads them at startup via `Azure.Extensions.AspNetCore.Configuration.Secrets` using `DefaultAzureCredential`. Never commit secret values to source control.

| Secret name | Description |
|---|---|
| `JwtSigningKey` | HMAC key used to sign and verify session JWTs |
| `TableStorageConnectionString` | Connection string for Azure Table Storage |
| `VapidPublicKey` | VAPID public key for Web Push |
| `VapidPrivateKey` | VAPID private key for Web Push |

For local development, set `KeyVaultUri` in `local.settings.json` and authenticate via `az login`.

## Project Structure

- All API calls from the client go through the Static Web Apps built-in proxy at `/api/*`
- Azure Functions read/write Azure Table Storage
- Push notifications are dispatched from Azure Functions
- The Service Worker handles offline caching and queues mutations when offline

## Pages and Routes

| Page | Route | Description |
|---|---|---|
| LoginPage | `/` | Password entry and housemate selection |
| DayPlanPage | `/day/{date}` | Full day plan: attendance, dish, comments, nudge, history |
| CalendarPage | `/calendar` | Calendar overview with color indicators |
| HousematesPage | `/housemates` | Housemate management (add, rename, remove, color) |

## Key Components

| Component | Description |
|---|---|
| `AttendanceToggle` | Three-state toggle (eating in / not eating in / unknown) per housemate |
| `DishEditor` | Inline editable field for the dish, max 100 chars |
| `CommentEditor` | Inline editable field for a housemate's comment slot, max 200 chars |
| `NudgeDialog` | Modal for selecting recipients and optional message (max 20 chars) |
| `CalendarGrid` | Month grid with color dot indicators per day |
| `DayHistoryLog` | Audit log of changes for a given day, shown in reverse-chronological order |
| `HousemateColorPicker` | Predefined palette of up to 30 colors |
| `OfflineBanner` | Shown when the app detects no network connectivity |

## API Conventions

All endpoints require:
- `Authorization: Bearer <jwt>`
- `X-Housemate-Id: <guid>`

Error responses follow this shape:
```json
{ "error": "Human-readable message", "code": "MACHINE_READABLE_CODE" }
```

Standard error codes: `UNAUTHORIZED` (401), `FORBIDDEN` (403), `NOT_FOUND` (404), `VALIDATION_ERROR` (422), `COLOR_CONFLICT` (409), `INTERNAL_ERROR` (500).

## Authentication Flow

1. User enters the household password → `POST /api/auth/login`
2. Server returns a signed JWT scoped to the matched `HouseholdId`
3. JWT stored in `localStorage`, sent as `Bearer` token on all requests
4. `ActiveHousemateId` stored separately in `localStorage`, sent as `X-Housemate-Id`
5. On return visits the stored JWT is validated; if still valid the user skips the password screen

## Azure Table Storage Schema

PartitionKey is always `HouseholdId` (string) so all records for a household are co-located.

| Table | PartitionKey | RowKey |
|---|---|---|
| `Households` | `"households"` | `{HouseholdId}` |
| `Housemates` | `{HouseholdId}` | `{HousemateId}` |
| `AttendanceRecords` | `{HouseholdId}` | `{YYYY-MM-DD}#{HousemateId}` |
| `DishRecords` | `{HouseholdId}` | `{YYYY-MM-DD}` |
| `Comments` | `{HouseholdId}` | `{YYYY-MM-DD}#{HousemateId}` |
| `DayHistory` | `{HouseholdId}` | `{YYYY-MM-DD}#{InvertedTimestamp}` |
| `PushSubscriptions` | `{HouseholdId}` | `{HousemateId}` |

`DayHistory` uses an inverted timestamp (`DateTimeOffset.MaxValue.Ticks - entry.ChangedAt.Ticks`) so entries are returned in reverse-chronological order by default.

## i18n

- Supported locales: `"en"` (English) and `"nl"` (Dutch)
- Default locale when none is set: `"nl"`
- Locale is persisted across sessions
- Language switches immediately without a page reload
- All source code, identifiers, and comments remain in English regardless of active locale
- Push subscription records store the housemate's locale so predefined nudge messages are resolved server-side in the recipient's language

## Testing Conventions

- Unit tests: xUnit
- Property-based tests: FsCheck, minimum 100 iterations per property
- Each property test must be tagged: `// Feature: happie, Property {N}: {property_text}`
- Both client-side and server-side validation must be enforced for all field length rules
