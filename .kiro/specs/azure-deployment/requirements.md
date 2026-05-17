# Requirements Document

## Introduction

This document defines the requirements for deploying the Happie application to Azure and setting up continuous deployment via GitHub Actions. The infrastructure includes an Azure Resource Group, Static Web Apps for the Blazor frontend, Azure Functions for the API backend, Azure Table Storage for data persistence, and Azure Key Vault for secrets management. A GitHub Actions workflow triggers on commits to the main branch and deploys both frontend and backend automatically.

## Glossary

- **Resource_Group**: An Azure Resource Group that acts as a logical container for all Happie Azure resources.
- **Static_Web_App**: An Azure Static Web Apps resource that hosts the Blazor WebAssembly PWA and provides the built-in `/api/*` proxy to the linked Azure Functions backend.
- **Functions_App**: An Azure Functions app (isolated worker model, .NET 10) that hosts the Happie API.
- **Storage_Account**: An Azure Storage Account that provides Table Storage for the application database.
- **Key_Vault**: An Azure Key Vault instance that stores application secrets accessed via Managed Identity.
- **Managed_Identity**: The system-assigned managed identity of the Functions_App, used to authenticate to Key_Vault without credentials.
- **Deployment_Workflow**: A GitHub Actions workflow that builds and deploys the application on commits to the main branch.
- **Deployment_Token**: The deployment token issued by Azure Static Web Apps, stored as a GitHub repository secret for authentication during deployment.
- **App_Settings**: Configuration values set on the Functions_App that the application reads at runtime (e.g., Key Vault URI).

## Requirements

### Requirement 1: Azure Resource Group

**User Story:** As a developer, I want all Happie Azure resources grouped in a single Resource Group, so that I can manage and monitor them as a unit.

#### Acceptance Criteria

1. THE Resource_Group SHALL be created in the West Europe Azure region.
2. THE Resource_Group SHALL contain all Azure resources defined in this specification (Static_Web_App, Functions_App, Storage_Account, Key_Vault).
3. THE Functions_App, Storage_Account, and Key_Vault SHALL be deployed to the same Azure region as the Resource_Group.

### Requirement 2: Azure Storage Account with Table Storage

**User Story:** As a developer, I want an Azure Storage Account provisioned, so that the application can persist data in Table Storage.

#### Acceptance Criteria

1. THE Storage_Account SHALL be created within the Resource_Group.
2. THE Storage_Account SHALL use the Standard performance tier with locally-redundant storage (LRS).
3. THE Storage_Account SHALL have Table Storage enabled with the following tables created: `Households`, `Housemates`, `AttendanceRecords`, `DishRecords`, `Comments`, `DayHistory`, and `PushSubscriptions`.
4. THE Storage_Account SHALL have its connection string stored in Key_Vault as the secret named `TableStorageConnectionString`.
5. THE Storage_Account SHALL enforce HTTPS-only access and require a minimum TLS version of 1.2.

### Requirement 3: Azure Key Vault

**User Story:** As a developer, I want an Azure Key Vault provisioned, so that application secrets are stored securely and accessed via Managed Identity.

#### Acceptance Criteria

1. THE Key_Vault SHALL be created within the Resource_Group.
2. THE Key_Vault SHALL store the following secrets: `JwtSigningKey`, `TableStorageConnectionString`, `VapidPublicKey`, `VapidPrivateKey`.
3. THE Key_Vault SHALL grant the Managed_Identity of the Functions_App the `Key Vault Secrets User` role at the vault scope, allowing it to read all secrets stored in the vault.
4. THE Key_Vault SHALL use Azure role-based access control (RBAC) as its permission model.
5. THE Key_Vault SHALL have soft-delete enabled with a retention period of 90 days and purge protection enabled.

### Requirement 4: Azure Functions App

**User Story:** As a developer, I want an Azure Functions app provisioned, so that the Happie API runs in the cloud with access to Key Vault secrets.

#### Acceptance Criteria

1. THE Functions_App SHALL be created within the Resource_Group using the isolated worker model on the Consumption (Serverless) plan.
2. THE Functions_App SHALL target the .NET 10 runtime.
3. THE Functions_App SHALL have a system-assigned Managed_Identity enabled.
4. THE Functions_App SHALL have an App_Setting named `KeyVaultUri` whose value is the full vault URI of the Key_Vault (e.g., `https://{vault-name}.vault.azure.net/`).
5. THE Key_Vault SHALL have an access policy that grants the Functions_App Managed_Identity the `Get` and `List` permissions on secrets.
6. WHEN the Functions_App starts, THE Functions_App SHALL authenticate to Key_Vault using DefaultAzureCredential via its Managed_Identity and load all Key_Vault secrets into the application configuration.
7. IF the Functions_App cannot reach Key_Vault or any required secret is missing at startup, THEN THE Functions_App SHALL fail to start and log an error message indicating which secret is unavailable.

### Requirement 5: Azure Static Web App

**User Story:** As a developer, I want an Azure Static Web App provisioned, so that the Blazor WebAssembly PWA is hosted and API calls are proxied to the Functions backend.

#### Acceptance Criteria

1. THE Static_Web_App SHALL be created within the Resource_Group on the Free tier.
2. THE Static_Web_App SHALL host the published Blazor WebAssembly output from the Happie.Web project.
3. THE Static_Web_App SHALL proxy requests matching `/api/*` to the linked Functions_App backend.
4. THE Static_Web_App SHALL be linked to the Functions_App as its backend.
5. THE Static_Web_App SHALL serve the Blazor WebAssembly application for all navigation routes that do not match a static file or `/api/*`, so that client-side routing to paths such as `/day/{date}`, `/calendar`, and `/housemates` resolves correctly without returning a 404 response.

### Requirement 6: GitHub Actions Deployment Workflow

**User Story:** As a developer, I want a GitHub Actions workflow that automatically builds and deploys the application when code is pushed to the main branch, so that deployments are consistent and hands-free.

#### Acceptance Criteria

1. WHEN a commit is pushed to the main branch, THE Deployment_Workflow SHALL trigger automatically.
2. THE Deployment_Workflow SHALL build the Happie.Web project in Release configuration and publish the Blazor WebAssembly output as a workflow artifact.
3. THE Deployment_Workflow SHALL build the Happie.Api project in Release configuration and publish the Azure Functions output as a workflow artifact.
4. THE Deployment_Workflow SHALL deploy the Blazor WebAssembly output to the Static_Web_App.
5. THE Deployment_Workflow SHALL deploy the Azure Functions output to the Functions_App.
6. THE Deployment_Workflow SHALL authenticate to Azure Static Web Apps using a Deployment_Token stored as a GitHub repository secret.
7. THE Deployment_Workflow SHALL authenticate to the Functions_App using a Publish_Profile stored as a GitHub repository secret.
8. THE Deployment_Workflow SHALL run the Happie.Api.Tests and Happie.Web.Tests projects before deployment and halt the workflow with a failure status if any test fails.
9. IF any build step fails, THEN THE Deployment_Workflow SHALL halt with a failure status without executing subsequent steps.

### Requirement 7: Infrastructure as Code

**User Story:** As a developer, I want the Azure infrastructure defined as code, so that it is reproducible, version-controlled, and reviewable.

#### Acceptance Criteria

1. THE infrastructure definition SHALL be stored in the repository as an Azure CLI script or Bicep template.
2. THE infrastructure definition SHALL create all resources defined in this specification (Resource_Group, Storage_Account, Key_Vault, Functions_App, Static_Web_App) and configure the App_Setting `KeyVaultUri` on the Functions_App pointing to the Key_Vault URI.
3. THE infrastructure definition SHALL configure the Managed_Identity role assignment on Key_Vault by assigning the `Key Vault Secrets User` role to the system-assigned Managed_Identity of the Functions_App.
4. THE infrastructure definition SHALL be idempotent: executing the script twice in succession against the same subscription SHALL result in no new resources created and no configuration changes applied on the second execution.
5. THE infrastructure definition SHALL configure the Static_Web_App linked backend to the Functions_App so that `/api/*` requests are proxied to the Functions_App.
6. THE infrastructure definition SHALL store the Storage_Account connection string in Key_Vault as the secret named `TableStorageConnectionString`.
