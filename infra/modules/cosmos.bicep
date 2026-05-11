param location string
param accountName string
param databaseName string

resource account 'Microsoft.DocumentDB/databaseAccounts@2024-08-15' = {
  name: accountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    capabilities: [
      { name: 'EnableServerless' }
    ]
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: true
    minimalTlsVersion: 'Tls12'
    enableAutomaticFailover: false
    enableMultipleWriteLocations: false
    backupPolicy: {
      type: 'Continuous'
      continuousModeProperties: { tier: 'Continuous7Days' }
    }
  }
}

resource db 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-08-15' = {
  parent: account
  name: databaseName
  properties: {
    resource: { id: databaseName }
  }
}

resource posts 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-08-15' = {
  parent: db
  name: 'posts'
  properties: {
    resource: {
      id: 'posts'
      partitionKey: {
        paths: [ '/postId' ]
        kind: 'Hash'
        version: 2
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [ { path: '/*' } ]
        excludedPaths: [ { path: '/"_etag"/?' } ]
      }
    }
  }
}

resource comments 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-08-15' = {
  parent: db
  name: 'comments'
  properties: {
    resource: {
      id: 'comments'
      partitionKey: {
        paths: [ '/postId' ]
        kind: 'Hash'
        version: 2
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [ { path: '/*' } ]
        excludedPaths: [ { path: '/"_etag"/?' } ]
      }
    }
  }
}

resource reactions 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-08-15' = {
  parent: db
  name: 'reactions'
  properties: {
    resource: {
      id: 'reactions'
      partitionKey: {
        paths: [ '/postId' ]
        kind: 'Hash'
        version: 2
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [ { path: '/*' } ]
        excludedPaths: [ { path: '/"_etag"/?' } ]
      }
    }
  }
}

resource agents 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-08-15' = {
  parent: db
  name: 'agents'
  properties: {
    resource: {
      id: 'agents'
      partitionKey: {
        paths: [ '/agentId' ]
        kind: 'Hash'
        version: 2
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [ { path: '/*' } ]
        excludedPaths: [ { path: '/"_etag"/?' } ]
      }
    }
  }
}

output accountName string = account.name
output databaseName string = db.name
output endpoint string = account.properties.documentEndpoint
output accountId string = account.id
