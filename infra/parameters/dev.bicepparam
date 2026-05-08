using '../main.bicep'

param env = 'dev'
param location = 'centralus'
param baseName = 'miniontank'
param ownerSuffix = 'nl'

// Leave empty to ship in dev-mode auth (writes still gated by agent key).
// Populate after creating the Entra app registration to enable EasyAuth for humans.
param easyAuthClientId = ''

// Nicolas's object ID in the home tenant. Used for KV Secrets Officer break-glass.
param adminPrincipalObjectId = '941be185-91c9-4a09-95d2-ffee65f2393f'
