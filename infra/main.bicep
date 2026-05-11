@description('Environment short name (e.g., dev, prod)')
param env string = 'dev'

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Base name for resources. Override only if rebranding.')
param baseName string = 'miniontank'

@description('Owner suffix for globally-unique names (App Service, Cosmos, KV).')
param ownerSuffix string = 'nl'

@description('Entra app registration clientId for EasyAuth. Leave empty to skip auth wiring; enable later via portal or rerun with the value set.')
param easyAuthClientId string = ''

@description('GitHub repository URL configured on the staging slot Deployment Center integration.')
param githubRepoUrl string = 'https://github.com/NicL9923/dotnet-test-app'

@description('GitHub branch configured on the staging slot Deployment Center integration.')
param githubBranch string = 'main'

@description('Client ID of the OIDC app registration created by App Service Deployment Center for staging deployments.')
param githubDeploymentClientId string = ''

@description('Object ID of the principal (you) that should retain admin access for break-glass scenarios.')
param adminPrincipalObjectId string

var appServiceName = 'app-${baseName}-${ownerSuffix}'
var planName = 'asp-${baseName}'
var cosmosAccountName = 'cosmos-${baseName}-${ownerSuffix}'
var cosmosDbName = baseName
var keyVaultName = 'kv-${baseName}-${ownerSuffix}'
var appInsightsName = 'appi-${baseName}'
var logAnalyticsName = 'log-${baseName}'

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    logAnalyticsName: logAnalyticsName
    appInsightsName: appInsightsName
  }
}

module cosmos 'modules/cosmos.bicep' = {
  name: 'cosmos'
  params: {
    location: location
    accountName: cosmosAccountName
    databaseName: cosmosDbName
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    location: location
    keyVaultName: keyVaultName
    adminPrincipalObjectId: adminPrincipalObjectId
  }
}

module appService 'modules/appservice.bicep' = {
  name: 'appservice'
  params: {
    location: location
    planName: planName
    appServiceName: appServiceName
    cosmosAccountName: cosmos.outputs.accountName
    cosmosDatabaseName: cosmos.outputs.databaseName
    keyVaultName: keyVault.outputs.name
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    easyAuthClientId: easyAuthClientId
    githubRepoUrl: githubRepoUrl
    githubBranch: githubBranch
    githubDeploymentClientId: githubDeploymentClientId
  }
}

output appServiceName string = appService.outputs.appServiceName
output appServiceDefaultHost string = appService.outputs.defaultHostName
output cosmosEndpoint string = cosmos.outputs.endpoint
output cosmosAccountName string = cosmos.outputs.accountName
output keyVaultName string = keyVault.outputs.name
output appInsightsConnectionString string = monitoring.outputs.appInsightsConnectionString
output appServicePrincipalId string = appService.outputs.principalId
