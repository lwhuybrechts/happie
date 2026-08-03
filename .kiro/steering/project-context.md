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

<!-- MAINTENANCE: when adding a new page, add a row to this table. -->

| Page | Route | Description |
|---|---|---|
| LoginPage | `/` | Password entry and housemate selection |
| DayPlanPage | `/day/{date}` | Full day plan: attendance, dishes, comments, nudge, history |
| CalendarPage | `/calendar` | Calendar overview with color indicators |
| HousematesPage | `/housemates` | Housemate management (add, rename, remove, color) |
| SavedDishesPage | `/saved-dishes` | Manage reusable saved dishes (add, rename, delete) |
| PushHelpPage | `/push-help` | Help page explaining how to enable push notifications |

The browser tab title MUST always be **Happie** on every page. Use `<PageTitle>Happie</PageTitle>` — do not append page-specific text or localized strings.

## Key Components

<!-- MAINTENANCE: when adding a new reusable component, add a row to this table. -->

| Component | Description |
|---|---|
| `AttendanceToggle` | Three-state toggle (eating in / not eating in / unknown) per housemate |
| `DishPanel` | Displays the day's dinner time, free-text dish description, and linked saved dishes with add/remove controls |
| `SavedDishModal` | Multi-select modal for linking saved dishes to a day plan |
| `CommentEditor` | Inline editable field for a housemate's comment slot, max 200 chars |
| `NudgeModal` | Modal for selecting recipients and optional message (max 20 chars) |
| `CalendarGrid` | Month grid with color dot indicators per day |
| `HistorySection` | Audit log of changes for a given day, shown in reverse-chronological order |
| `HousemateColorPicker` | Predefined palette of up to 30 colors |
| `ColorPickerModal` | Modal wrapper for housemate color selection |
| `DateNavigationPanel` | Swipeable date navigation with today/yesterday shortcuts |
| `OfflineBanner` | Shown when the app detects no network connectivity |
| `SyncToast` | Toast notification showing offline sync status and failures |
| `LoadingIndicator` | Spinner shown during background API operations |
| `LocaleSwitcher` | Language toggle between Dutch and English |
| `HousemateAvatar` | Colored circle with housemate initial |

## API Conventions

All endpoints require:
- `Authorization: Bearer <jwt>`
- `X-Housemate-Id: <guid>`

Error responses follow this shape:
```json
{ "error": "Human-readable message", "code": "MACHINE_READABLE_CODE" }
```

The response body is typed as `ApiErrorResponse(string Error, string Code)` with `[JsonPropertyName]` attributes for lowercase wire format. Error codes are defined as constants in `ApiErrorCodes`:

| Code | Status | Meaning |
|---|---|---|
| `UNAUTHORIZED` | 401 | Missing or invalid credentials |
| `FORBIDDEN` | 403 | Authenticated but not permitted |
| `NOT_FOUND` | 404 | Resource does not exist |
| `VALIDATION_ERROR` | 422 | Request payload failed validation |
| `COLOR_CONFLICT` | 409 | Requested housemate color already in use |
| `BAD_REQUEST` | 400 | Malformed or missing request body |

Unhandled enum values in switch expressions throw `InvalidOperationException` rather than returning a 500 response.

## Authentication Flow

1. User enters the household password → `POST /api/auth/login`
2. Server returns a signed JWT scoped to the matched `HouseholdId`
3. JWT stored in `localStorage`, sent as `Bearer` token on all requests
4. `ActiveHousemateId` stored separately in `localStorage`, sent as `X-Housemate-Id`
5. On return visits the stored JWT is validated; if still valid the user skips the password screen

## Running Locally

When asked to start the app or run locally, ALWAYS read `.kiro/steering/local-dev.md` first and follow the instructions there. That file contains the full startup procedure, ports, seeding, and troubleshooting details.

## Steering File Index

<!-- MAINTENANCE: when adding a new steering file, add a row to this table. -->

Additional conventions are loaded automatically when working on relevant files. When you need guidance on a topic without a matching file in context, read the relevant steering file:

| File | Topic | Loaded when |
|---|---|---|
| `api-conventions.md` | Functions, request validation, options pattern | Editing `Happie.Api/` or `Happie.Shared/` |
| `entity-conventions.md` | Entities, repositories, mappers, enum storage | Editing `Infrastructure/` or `Happie.Shared/Domain/` |
| `testing-conventions.md` | xUnit, FsCheck, test naming, assertions | Editing `*Tests*/` |
| `bunit-testing.md` | bUnit component test patterns | Editing `Happie.Web.Tests/` |
| `ui-conventions.md` | Blazor patterns, modals, CSS | Editing `.razor` / `Happie.Web/` |
| `i18n-conventions.md` | Localization, resx files, SharedStringResolver | Editing `.resx` / `.razor` / API / Shared |
| `offline-cache-conventions.md` | IndexedDB cache, CachedApiClient, sync | Editing `Caching/` / `cacheDb` |
| `domain-rules.md` | Business rules, validation, entity lifecycles | Editing Handlers / Functions / Domain / Components |
| `local-dev.md` | Full local dev setup, seeding, prerequisites | Starting the app or running locally |
| `infrastructure.md` | Azure resources, Bicep, deployment | Manual (`#infrastructure`) |
| `lan-testing.md` | iPhone LAN testing setup | Manual (`#lan-testing`) |
