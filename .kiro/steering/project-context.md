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

The browser tab title MUST always be **Happie** on every page. Use `<PageTitle>Happie</PageTitle>` — do not append page-specific text or localized strings.

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

## Azure Table Storage Schema

PartitionKey is always `HouseholdId` (string) so all records for a household are co-located.

| Table | PartitionKey | RowKey |
|---|---|---|
| `Households` | `"households"` | `{HouseholdId}` |
| `Housemates` | `{HouseholdId}` | `{HousemateId}` |
| `AttendanceRecords` | `{HouseholdId}` | `{YYYY-MM-DD}_{HousemateId}` |
| `DishRecords` | `{HouseholdId}` | `{YYYY-MM-DD}` |
| `Comments` | `{HouseholdId}` | `{YYYY-MM-DD}_{HousemateId}` |
| `DayHistory` | `{HouseholdId}` | `{YYYY-MM-DD}_{InvertedTimestamp}` |
| `PushSubscriptions` | `{HouseholdId}` | `{HousemateId}` |

`DayHistory` uses an inverted timestamp (`DateTimeOffset.MaxValue.Ticks - entry.ChangedAt.Ticks`) so entries are returned in reverse-chronological order by default.

## i18n

- Supported locales: `"en"` (English) and `"nl"` (Dutch)
- Default locale when none is set: `"nl"`
- Locale is persisted across sessions
- Language switches immediately without a page reload
- All source code, identifiers, and comments remain in English regardless of active locale
- Push subscription records store the housemate's locale so predefined nudge messages are resolved server-side in the recipient's language
- **All user-visible strings MUST use `IStringLocalizer<AppStrings>`** — NEVER hardcode English text directly in `.razor` components or service classes. This includes labels like "Today"/"Yesterday", relative time strings like "min ago", section headers, button text, placeholders, and error messages. Add keys to both `AppStrings.resx` (Dutch) and `AppStrings.en.resx` (English). For static utility classes that cannot inject `IStringLocalizer`, accept localized strings as method parameters.

## Testing Conventions

- Unit tests: xUnit
- Property-based tests: FsCheck, minimum 100 iterations per property
- Each property test must be tagged: `// Feature: happie, Property {N}: {property_text}`
- Both client-side and server-side validation must be enforced for all field length rules

## Running Locally

### Prerequisites

- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local) (`func` on PATH)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) running on default ports (Table: 10002)
- `az login` is only required if `KeyVaultUri` is set in `local.settings.json`; if it is absent the app skips Key Vault entirely and reads secrets directly from `local.settings.json`

### Start the API

```bash
cd Happie.Api
func start
```

The API starts on **http://localhost:7071**. All function endpoints are listed in the startup output.

The `local.settings.json` includes a `Host.CORS` entry that allows requests from the Blazor dev server (`http://localhost:5195`). This is required because the browser enforces CORS when the frontend and API run on different ports locally.

### Start the Frontend

```bash
dotnet run --project Happie.Web --launch-profile http
```

The frontend starts on **http://localhost:5195**.

### Applying Changes During Development

Blazor WebAssembly does NOT support hot reload for `.razor` or `.razor.css` file changes. A browser hard refresh (Ctrl+Shift+R) is not sufficient to pick up changes. After making code or CSS changes, you MUST stop and restart the frontend dev server (`dotnet run --project Happie.Web --launch-profile http`) for the changes to take effect. The same applies to the API (`func start`) when backend code changes.

### Local Test Data — Seed a Household

The `Households` table in Azurite must contain at least one record before login works. Run the seed script to insert a test household and housemates:

```bash
dotnet-script Happie.Api.IntegrationTests/Scripts/seed-local.csx
```

This inserts a test household (password: **`happie`**) with two housemates (Alice and Bob). The script is idempotent (uses upsert), so it's safe to run after integration tests truncate the tables or after restarting Azurite.


## Blazor WebAssembly Patterns

### Locale switching — forceLoad pattern

Blazor WASM's `ResourceManager` caches satellite assemblies per culture and cannot switch them mid-session. The only reliable way to change the active locale at runtime is to persist the choice and reload the page.

**Pattern:**
1. Persist the new locale via `LocaleService.SetLocaleAsync(locale)` (writes to `localStorage`)
2. Call `NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true)` to reload
3. On startup, `Program.cs` reads the stored locale via `LocaleService.InitializeAsync()` and sets `CultureInfo.DefaultThreadCurrentCulture` / `DefaultThreadCurrentUICulture` before rendering

**Preserving component state across reload:**
If the page has in-memory state that must survive the reload (e.g., a list fetched from the API), store it in `sessionStorage` before the reload and read it back in `OnInitializedAsync`. Clean up `sessionStorage` once the state is no longer needed.

### CSS isolation — `::deep` for child component elements

Blazor's scoped CSS adds a unique attribute to elements rendered directly by the component, but NOT to elements rendered by child Blazor components (e.g., `<InputText>` renders an `<input>`). To style those inner elements, use the `::deep` combinator in the scoped `.razor.css` file.

### Login page auto-redirect guard

The login page (`/`) checks for an existing session on load. It MUST only redirect to the day plan if **both** conditions are met:
- `jwt` exists in `localStorage` (user has authenticated)
- `activeHousemateId` exists in `localStorage` (user has selected a housemate)

If only the JWT exists (e.g., user is on the housemate selection step and reloads), the page MUST show the housemate selection view, not redirect.
