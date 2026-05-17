---
inclusion: manual
---

# Happie — Azure Infrastructure

## Security Note

This file is included in the repository. Do NOT add secrets, connection strings, account keys, or deployment tokens here. Tenant IDs, subscription IDs, and resource names are not authentication secrets, but for a public repository it is best practice to omit them to reduce the attack surface (reconnaissance). Reference them by variable or retrieve them via CLI commands instead.

## Resource Naming Convention

All resources use the base name `happie` with suffixes:

| Resource | Naming Pattern |
|---|---|
| Resource Group | `rg-happie` |
| Storage Account | `happiestorage` |
| Key Vault | `happie-kv` |
| App Service Plan | `happie-plan` |
| Functions App | `happie-func` |
| Static Web App | `happie-swa` |

## Key Design Decisions

- **Static Web App tier**: Free. The Free tier does NOT support linked backends, so the frontend calls the Functions App directly (not via `/api/*` proxy). CORS is configured on the Functions App to allow the SWA hostname.
- **API base URL**: The production `Happie.Web/wwwroot/appsettings.json` points `ApiBaseUrl` directly to the Functions App hostname. If the Functions App name changes, update this file.
- **Bicep template**: Single file at `infra/main.bicep`. Deploys all resources except Table Storage tables (those require the separate `infra/create-tables.sh` script).
- **Region**: West Europe.

## Key Vault Secrets

| Secret | Description |
|---|---|
| `JwtSigningKey` | HMAC key for JWT signing |
| `TableStorageConnectionString` | Full connection string for the Storage Account |
| `VapidPublicKey` | VAPID public key for Web Push |
| `VapidPrivateKey` | VAPID private key for Web Push |
| `SentryDsn` | Sentry Data Source Name for error monitoring |

## Common Infrastructure Commands

### Login (tenant requires MFA)

```bash
az login --tenant <TENANT_ID>
```

### Deploy Bicep template

```bash
# Get the current connection string.
CONNECTION_STRING=$(az storage account show-connection-string --name happiestorage --resource-group rg-happie --query connectionString -o tsv)

# Deploy.
az deployment group create --resource-group rg-happie --template-file infra/main.bicep --parameters tableStorageConnectionString="$CONNECTION_STRING"
```

### Create Table Storage tables (idempotent)

```bash
bash infra/create-tables.sh happiestorage
```

### Retrieve GitHub secrets (if they need to be re-set)

```bash
# Static Web App deployment token.
az staticwebapp secrets list --name happie-swa --query "properties.apiKey" -o tsv

# Functions App publish profile.
az functionapp deployment list-publishing-profiles --name happie-func --resource-group rg-happie --xml
```

### Set a Key Vault secret

```bash
az keyvault secret set --vault-name happie-kv --name SecretName --value "secret-value"
```

## GitHub Actions Secrets

| Secret | Source |
|---|---|
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | SWA deployment token (see command above) |
| `AZURE_FUNCTIONAPP_PUBLISH_PROFILE` | Functions publish profile XML (see command above) |

## CORS Configuration

CORS is set in the Bicep template on the Functions App (`siteConfig.cors`):
- The SWA default hostname (production)
- `http://localhost:5195` (local development)

If the SWA hostname changes (e.g., custom domain), update the CORS allowed origins in `infra/main.bicep` and redeploy.
