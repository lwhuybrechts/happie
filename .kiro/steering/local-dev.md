---
inclusion: manual
---

# Happie — Local Development

## Prerequisites

- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local) (`func` on PATH)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) running on default ports (Table: 10002)
- `az login` is only required if `KeyVaultUri` is set in `local.settings.json`; if it is absent the app skips Key Vault entirely and reads secrets directly from `local.settings.json`

## Start the Full App

When asked to "start the app" or "run locally", start **all three** processes in this order:

1. **Azurite** (Table Storage emulator — must be running before the API starts):
   ```bash
   azurite --silent
   ```

2. **API** (Azure Functions):
   ```bash
   cd Happie.Api
   func start
   ```
   The API starts on **http://localhost:7071**. All function endpoints are listed in the startup output.

3. **Frontend** (Blazor WASM):
   ```bash
   dotnet run --project Happie.Web --launch-profile http
   ```
   The frontend starts on **http://localhost:5195**.

The `local.settings.json` includes a `Host.CORS` entry that allows requests from the Blazor dev server (`http://localhost:5195`). This is required because the browser enforces CORS when the frontend and API run on different ports locally.

## Applying Changes During Development

Blazor WebAssembly does NOT support hot reload for `.razor` or `.razor.css` file changes. A browser hard refresh (Ctrl+Shift+R) is not sufficient to pick up changes. After making code or CSS changes, you MUST stop and restart the frontend dev server (`dotnet run --project Happie.Web --launch-profile http`) for the changes to take effect. The same applies to the API (`func start`) when backend code changes.

## Local Test Data — Seed a Household

The `Households` table in Azurite must contain at least one record before login works. Run the seed script to insert a test household and housemates:

```bash
dotnet-script Happie.Api.IntegrationTests/Scripts/seed-local.csx
```

This inserts a test household (password: **`happie`**) with two housemates (Alice and Bob). The script is idempotent (uses upsert), so it's safe to run after integration tests truncate the tables or after restarting Azurite.

## Re-seed After Integration Tests

Integration tests truncate Azure Table Storage tables as part of their setup. This leaves the local database empty after a test run, which breaks manual testing (login fails because no household exists).

**After running integration tests, ALWAYS re-seed the database:**

```bash
dotnet-script Happie.Api.IntegrationTests/Scripts/seed-local.csx
```

When asked to run integration tests (e.g., `dotnet test` on the integration test project), always follow up with the seed script so the local environment remains usable for manual testing.
