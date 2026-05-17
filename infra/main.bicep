// Happie Azure Infrastructure — Bicep template.
// Deploys all Azure resources for the Happie application.

@description('Azure region for all resources.')
param location string = 'westeurope'

@description('Base name used to derive resource names.')
param appName string = 'happie'

@description('Connection string for Azure Table Storage, stored in Key Vault.')
@secure()
param tableStorageConnectionString string

// Storage Account — Standard LRS, HTTPS-only, TLS 1.2 minimum.
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: '${appName}storage'
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
}

// Key Vault — RBAC permission model, soft-delete 90 days, purge protection enabled.
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${appName}-kv'
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
  }
}

// Store the Table Storage connection string in Key Vault.
resource tableStorageConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'TableStorageConnectionString'
  properties: {
    value: tableStorageConnectionString
  }
}

// App Service Plan — Consumption (Serverless) Y1 tier.
resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${appName}-plan'
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  kind: 'functionapp'
}

// Functions App — isolated worker model, .NET 10 runtime, system-assigned Managed Identity.
resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: '${appName}-func'
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v10.0'
      appSettings: [
        {
          name: 'KeyVaultUri'
          value: keyVault.properties.vaultUri
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: '${appName}-func'
        }
      ]
      cors: {
        allowedOrigins: [
          'https://${staticWebApp.properties.defaultHostname}'
          'https://happie.dev'
          'http://localhost:5195'
        ]
        supportCredentials: true
      }
    }
  }
}

// Role Assignment — Key Vault Secrets User role for the Functions App Managed Identity on the Key Vault.
resource keyVaultSecretsUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, functionApp.id, '4633458b-17de-408a-b874-0445c86b69e6')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Static Web App — Free tier, hosts the Blazor WebAssembly PWA.
resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: '${appName}-swa'
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

// Custom domain — apex domain validated via TXT record.
resource customDomain 'Microsoft.Web/staticSites/customDomains@2023-12-01' = {
  parent: staticWebApp
  name: 'happie.dev'
  properties: {
    validationMethod: 'dns-txt-token'
  }
}

// Note: Linked backends require Standard tier. With Free tier, the frontend calls
// the Functions App directly. CORS is configured on the Functions App to allow
// requests from the Static Web App hostname.

// Template outputs.
output staticWebAppDefaultHostname string = staticWebApp.properties.defaultHostname
output functionAppName string = functionApp.name
output functionAppDefaultHostname string = functionApp.properties.defaultHostName
output staticWebAppName string = staticWebApp.name
