param location string
param planName string
param appServiceName string

@description('Cosmos account name (in this RG) — used for RBAC role assignment + app setting.')
param cosmosAccountName string
param cosmosDatabaseName string

@description('Key Vault name (in this RG) — used for RBAC role assignment.')
param keyVaultName string

@description('Application Insights connection string for telemetry.')
param appInsightsConnectionString string

@description('Entra app registration clientId for EasyAuth. If empty, EasyAuth is left disabled and the resource ships in "dev" auth mode.')
param easyAuthClientId string = ''

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: planName
  location: location
  kind: 'app'
  sku: {
    name: 'S1'
    tier: 'Standard'
  }
  properties: {
    reserved: false
  }
}

resource site 'Microsoft.Web/sites@2024-04-01' = {
  name: appServiceName
  location: location
  kind: 'app'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      netFrameworkVersion: 'v10.0'
      alwaysOn: true
      ftpsState: 'FtpsOnly'
      http20Enabled: true
      minTlsVersion: '1.2'
      healthCheckPath: '/healthz'
      use32BitWorkerProcess: false
      defaultDocuments: [ 'index.html' ]
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'Cosmos__Endpoint'
          value: 'https://${cosmosAccountName}.documents.azure.com:443/'
        }
        {
          name: 'Cosmos__DatabaseId'
          value: cosmosDatabaseName
        }
        {
          name: 'Cosmos__AccountName'
          value: cosmosAccountName
        }
        {
          name: 'KeyVault__Name'
          value: keyVaultName
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'Auth__DevMode'
          value: empty(easyAuthClientId) ? 'true' : 'false'
        }
      ]
    }
  }
}

resource stagingSlot 'Microsoft.Web/sites/slots@2024-04-01' = {
  parent: site
  name: 'staging'
  location: location
  kind: 'app'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      netFrameworkVersion: 'v10.0'
      alwaysOn: true
      ftpsState: 'FtpsOnly'
      http20Enabled: true
      minTlsVersion: '1.2'
      healthCheckPath: '/healthz'
      use32BitWorkerProcess: false
      defaultDocuments: [ 'index.html' ]
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Staging'
        }
        {
          name: 'Cosmos__Endpoint'
          value: 'https://${cosmosAccountName}.documents.azure.com:443/'
        }
        {
          name: 'Cosmos__DatabaseId'
          value: cosmosDatabaseName
        }
        {
          name: 'Cosmos__AccountName'
          value: cosmosAccountName
        }
        {
          name: 'KeyVault__Name'
          value: keyVaultName
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'Auth__DevMode'
          value: empty(easyAuthClientId) ? 'true' : 'false'
        }
      ]
    }
  }
}

resource auth 'Microsoft.Web/sites/config@2024-04-01' = if (!empty(easyAuthClientId)) {
  parent: site
  name: 'authsettingsV2'
  properties: {
    globalValidation: {
      requireAuthentication: true
      unauthenticatedClientAction: 'RedirectToLoginPage'
      excludedPaths: [
        '/api/*'
        '/healthz'
        '/openapi/*'
      ]
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        registration: {
          openIdIssuer: '${environment().authentication.loginEndpoint}${subscription().tenantId}/v2.0'
          clientId: easyAuthClientId
        }
        validation: {
          allowedAudiences: [ 'api://${easyAuthClientId}' ]
        }
      }
    }
    login: {
      tokenStore: {
        enabled: true
      }
      preserveUrlFragmentsForLogins: true
    }
    httpSettings: {
      requireHttps: true
    }
  }
}

resource authStaging 'Microsoft.Web/sites/slots/config@2024-04-01' = if (!empty(easyAuthClientId)) {
  parent: stagingSlot
  name: 'authsettingsV2'
  properties: {
    globalValidation: {
      requireAuthentication: true
      unauthenticatedClientAction: 'RedirectToLoginPage'
      excludedPaths: [
        '/api/*'
        '/healthz'
        '/openapi/*'
      ]
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        registration: {
          openIdIssuer: '${environment().authentication.loginEndpoint}${subscription().tenantId}/v2.0'
          clientId: easyAuthClientId
        }
        validation: {
          allowedAudiences: [ 'api://${easyAuthClientId}' ]
        }
      }
    }
    login: {
      tokenStore: {
        enabled: true
      }
      preserveUrlFragmentsForLogins: true
    }
    httpSettings: {
      requireHttps: true
    }
  }
}

@description('Cosmos data plane RBAC: Built-in Cosmos DB Built-in Data Contributor.')
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-08-15' existing = {
  name: cosmosAccountName
}

resource cosmosRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-08-15' = {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, site.id, 'data-contributor')
  properties: {
    roleDefinitionId: '${cosmosAccount.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
    principalId: site.identity.principalId
    scope: cosmosAccount.id
  }
}

resource cosmosRoleAssignmentSlot 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-08-15' = {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, stagingSlot.id, 'data-contributor')
  properties: {
    roleDefinitionId: '${cosmosAccount.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
    principalId: stagingSlot.identity.principalId
    scope: cosmosAccount.id
  }
}

@description('Key Vault Secrets User role for the production slot identity.')
resource kv 'Microsoft.KeyVault/vaults@2024-04-01-preview' existing = {
  name: keyVaultName
}

resource kvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: kv
  name: guid(kv.id, site.id, 'kv-secrets-user')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: site.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource kvRoleAssignmentSlot 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: kv
  name: guid(kv.id, stagingSlot.id, 'kv-secrets-user')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: stagingSlot.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output appServiceName string = site.name
output defaultHostName string = site.properties.defaultHostName
output principalId string = site.identity.principalId
output stagingPrincipalId string = stagingSlot.identity.principalId
