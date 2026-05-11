using '../main.bicep'

param env = 'dev'
param location = 'centralus'
param baseName = 'miniontank'
param ownerSuffix = 'aux'

// Entra app registration in the App Service UX tenant — created via az ad app create.
param easyAuthClientId = '685cc7c8-109f-42af-8b69-b45fc95ed8ee'

// Nicolas's object ID in the App Service UX tenant.
param adminPrincipalObjectId = 'f122ca93-a02f-4239-b493-6d6aba20f435'
