param location string
param keyVaultName string
param adminPrincipalObjectId string

resource kv 'Microsoft.KeyVault/vaults@2024-04-01-preview' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled'
  }
}

@description('Built-in: Key Vault Secrets Officer (read/write secrets) for the deployer/admin.')
resource adminAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: kv
  name: guid(kv.id, adminPrincipalObjectId, 'kv-secrets-officer')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7')
    principalId: adminPrincipalObjectId
    principalType: 'User'
  }
}

output name string = kv.name
output id string = kv.id
output uri string = kv.properties.vaultUri
