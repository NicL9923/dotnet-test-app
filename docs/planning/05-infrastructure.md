# 05 — Infrastructure (Bicep)

**Status:** Draft

## Goal
A single Bicep deployment defines every Azure resource the app uses, including the existing `nl-testdotnetwebapp`. After this lands, the portal-created webapp is reverse-engineered into source-controlled IaC and Bicep is the source of truth.

## Layout
```
infra/
├── main.bicep                     // entry point, parameters, environment-specific values
├── modules/
│   ├── appservice.bicep           // plan + webapp + slot + EasyAuth + identity
│   ├── cosmos.bicep               // account + database + 4 containers
│   ├── keyvault.bicep             // vault + role assignments to webapp identity
│   └── monitoring.bicep           // (phase 2) App Insights + Log Analytics workspace
└── parameters/
    ├── dev.bicepparam
    └── prod.bicepparam
```

## Resource map

| Resource | Name pattern | Notes |
| --- | --- | --- |
| Resource group | `rg-agentsocial-<env>` | one per env |
| App Service plan | `asp-agentsocial-<env>` | Windows, B1 to start, S1 if we need slots/scale |
| App Service | `nl-testdotnetwebapp` (existing) | rename later if we want; keep for now |
| Slot | `staging` | already exists; deploy target |
| User-assigned managed identity | optional; default to **system-assigned** on the webapp | simpler; Bicep manages role assignments |
| Cosmos DB account | `cosmos-agentsocial-<env>` | `Standard` tier, **serverless** capacity, single write region |
| Cosmos DB | `agentsocial` | inside the account |
| Cosmos containers | `posts`, `comments`, `reactions`, `agents` | partition keys per data-model doc |
| Key Vault | `kv-agtsocial-<env>` | RBAC mode (not access policies); soft-delete + purge protection on |
| Diagnostic settings | webapp + cosmos → Log Analytics | phase 2 |

## Authentication & secret strategy

### No connection strings
The webapp's system-assigned managed identity gets:
- **Cosmos DB Built-in Data Contributor** role on the Cosmos account.
- **Key Vault Secrets User** role on the Key Vault.

The app uses `Microsoft.Azure.Cosmos` SDK with `DefaultAzureCredential` — no keys, no connection strings, ever in app settings or anywhere else.

### What goes in Key Vault
- *Hint:* almost nothing in v1. Cosmos auth is identity-based, agent keys are stored hashed in Cosmos, EasyAuth manages its own client secret in App Service config.
- Reserve KV for things like:
  - Optional outbound webhook signing key.
  - Future: SMTP creds, third-party API keys.

### App settings
Plain (non-secret) app settings only. Examples:
- `Cosmos__Endpoint = https://cosmos-agentsocial-<env>.documents.azure.com:443/`
- `Cosmos__DatabaseId = agentsocial`
- `Auth__TenantId = <tenant guid>` (for token-store config; not a secret)
- `ASPNETCORE_ENVIRONMENT = Production`

## EasyAuth config (in Bicep)
Configured via `Microsoft.Web/sites/config@2024-04-01` with `name: 'authsettingsV2'`:
- `globalValidation.requireAuthentication: true`
- `globalValidation.unauthenticatedClientAction: 'RedirectToLoginPage'`
- `globalValidation.excludedPaths: ['/api/*', '/healthz', '/openapi/*']` — those return 401 instead of redirect (the app handles them).
- `identityProviders.azureActiveDirectory.enabled: true`
- `identityProviders.azureActiveDirectory.registration.clientId` — Entra app reg, **single-tenant**.
- `identityProviders.azureActiveDirectory.validation.allowedAudiences: ['api://<clientId>']`.
- `login.tokenStore.enabled: true`.
- `login.preserveUrlFragmentsForLogins: true`.

## Reverse-engineering the existing webapp
Process:
1. `az webapp show -n nl-testdotnetwebapp -g <rg> -o json` → capture current SKU, runtime, slot config, app settings, auth config.
2. Translate into `appservice.bicep` parameters/values.
3. Run `bicep what-if` against the existing RG and reconcile drift before applying.
4. First apply: expect no-op or trivial deltas. Subsequent changes flow through Bicep only.

## Pipeline integration
- Add a **separate** GitHub Actions workflow `infra.yml`:
  - Triggers: changes under `infra/**` or `workflow_dispatch`.
  - Steps: `azure/login@v2` (existing OIDC creds), `az deployment group create` with `what-if` first, then full deploy if approved.
  - Use the existing federated identity client; just grant it `Contributor` + `User Access Administrator` (for role assignments) on `rg-agentsocial-<env>`. The latter is needed because Bicep creates role assignments.

## Out of scope (v1)
- Front Door / WAF.
- VNet integration / private endpoints for Cosmos and KV.
- Multi-region.
- Backup/restore policies.

These are phase 4 items.
