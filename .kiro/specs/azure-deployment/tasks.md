# Implementation Plan: Azure Deployment

## Overview

This plan implements the Azure infrastructure and CI/CD pipeline for the Happie application. It uses Azure Bicep to define all cloud resources declaratively and a GitHub Actions workflow to build, test, and deploy the application automatically on commits to the `main` branch. The implementation is split into: Bicep template creation, table initialization script, Static Web App routing configuration, GitHub Actions workflow, and final wiring/validation.

## Tasks

- [x] 1. Create Bicep infrastructure template
  - [x] 1.1 Create `infra/main.bicep` with parameters, Storage Account, and Key Vault
    - Create the `infra/` directory and `main.bicep` file
    - Define parameters: `location` (default: `westeurope`), `appName` (default: `happie`), `tableStorageConnectionString` (securestring)
    - Define the Storage Account resource: Standard_LRS, HTTPS-only, TLS 1.2 minimum
    - Define the Key Vault resource: RBAC permission model, soft-delete 90 days, purge protection enabled
    - Store `TableStorageConnectionString` secret in Key Vault
    - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.2, 2.5, 3.1, 3.2, 3.4, 3.5_

  - [x] 1.2 Add Functions App with Managed Identity and Key Vault role assignment to `infra/main.bicep`
    - Define the Consumption (Serverless) App Service Plan
    - Define the Functions App resource: isolated worker model, .NET 10 runtime, system-assigned Managed Identity
    - Configure App Settings: `KeyVaultUri`, `FUNCTIONS_WORKER_RUNTIME`, `FUNCTIONS_EXTENSION_VERSION`, `AzureWebJobsStorage`, `WEBSITE_CONTENTAZUREFILECONNECTIONSTRING`, `WEBSITE_CONTENTSHARE`
    - Define the role assignment: `Key Vault Secrets User` role for the Functions App Managed Identity on the Key Vault
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 3.3, 7.2, 7.3_

  - [x] 1.3 Add Static Web App with linked backend to `infra/main.bicep`
    - Define the Static Web App resource: Free tier
    - Define the `Microsoft.Web/staticSites/linkedBackends` resource linking SWA to the Functions App
    - Add template outputs: `staticWebAppDefaultHostname`, `functionAppName`, `staticWebAppName`
    - _Requirements: 5.1, 5.3, 5.4, 7.5_

- [x] 2. Create table initialization script and Static Web App configuration
  - [x] 2.1 Create `infra/create-tables.sh` for Table Storage table creation
    - Write an idempotent Azure CLI script that creates all required tables: `Households`, `Housemates`, `AttendanceRecords`, `DishRecords`, `Comments`, `DayHistory`, `PushSubscriptions`
    - Accept the Storage Account name as a parameter
    - Use `az storage table create` which succeeds if the table already exists
    - _Requirements: 2.3, 7.4_

  - [x] 2.2 Create `Happie.Web/staticwebapp.config.json` for client-side routing
    - Configure `navigationFallback` to rewrite to `/index.html`
    - Exclude `/css/*`, `/js/*`, `/_framework/*`, `/api/*` from the fallback
    - Add route rule for `/api/*` allowing anonymous access
    - _Requirements: 5.2, 5.5_

- [x] 3. Checkpoint - Validate Bicep template
  - Ensure `az bicep build --file infra/main.bicep` succeeds without errors, ask the user if questions arise.

- [x] 4. Create GitHub Actions deployment workflow
  - [x] 4.1 Create `.github/workflows/deploy.yml` with build-and-test job
    - Define workflow triggers: `push` on `main` branch and `workflow_dispatch` with optional `environment` input
    - Define the `build-and-test` job: checkout, setup .NET 10, restore, build (Release configuration)
    - Add test steps: run `Happie.Api.Tests`, `Happie.Web.Tests` (both `--configuration Release --no-build`)
    - Add Azurite steps: install via npm, start as background process
    - Add integration test step: run `Happie.Api.IntegrationTests` (`--configuration Release --no-build`)
    - Add publish steps: `dotnet publish Happie.Web` and `dotnet publish Happie.Api` in Release configuration
    - Upload both publish outputs as workflow artifacts
    - _Requirements: 6.1, 6.2, 6.3, 6.8, 6.9_

  - [x] 4.2 Add `deploy-swa` and `deploy-functions` jobs to `.github/workflows/deploy.yml`
    - Define `deploy-swa` job: depends on `build-and-test`, downloads Blazor WASM artifact, deploys to Static Web App using `Azure/static-web-apps-deploy` action with `AZURE_STATIC_WEB_APPS_API_TOKEN` secret
    - Define `deploy-functions` job: depends on `build-and-test`, downloads Functions artifact, deploys to Functions App using `Azure/functions-action` with `AZURE_FUNCTIONAPP_PUBLISH_PROFILE` secret
    - _Requirements: 6.4, 6.5, 6.6, 6.7_

- [x] 5. Final checkpoint - Validate all files
  - Ensure `az bicep build --file infra/main.bicep` succeeds, validate the YAML workflow syntax, and confirm `staticwebapp.config.json` is valid JSON. Ask the user if questions arise.

## Notes

- This feature has no property-based tests because it consists entirely of Infrastructure as Code (Bicep), CI/CD pipeline configuration (YAML), and a routing config file (JSON). There are no pure functions or data transformations to test with PBT.
- The Bicep template is idempotent by design — running it twice produces no changes on the second execution (Requirement 7.4).
- The `create-tables.sh` script must be run manually after the initial Bicep deployment since Bicep cannot create individual Table Storage tables.
- GitHub repository secrets (`AZURE_STATIC_WEB_APPS_API_TOKEN` and `AZURE_FUNCTIONAPP_PUBLISH_PROFILE`) must be configured manually in the GitHub repository settings after the Azure resources are provisioned.
- The `workflow_dispatch` trigger allows ad-hoc deployments from any branch for testing purposes.
- Key Vault secrets (`JwtSigningKey`, `VapidPublicKey`, `VapidPrivateKey`) must be populated manually or via a separate script after initial deployment. Only `TableStorageConnectionString` is set by the Bicep template.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.2"] },
    { "id": 1, "tasks": ["1.2", "2.1"] },
    { "id": 2, "tasks": ["1.3"] },
    { "id": 3, "tasks": ["4.1"] },
    { "id": 4, "tasks": ["4.2"] }
  ]
}
```
