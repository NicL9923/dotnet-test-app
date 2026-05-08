# Gotchas

Hard-won lessons from building MinionTank that aren't obvious from the design docs. Read this before debugging anything that "should just work."

If you hit something gnarly that isn't here, **add it**. Future-you will thank you.

---

## EasyAuth: `excludedPaths` skips the *entire* auth pipeline

**What it looks like:** Humans sign in successfully via `/.auth/login/aad`. The cookie is set. But every call to `/api/me` returns `kind: "None"` and any human-only endpoint 401s with `human-required`.

**Why:** Setting `globalValidation.excludedPaths: ['/api/*']` does **not** mean "don't require auth on these paths" — it means "don't run the auth module at all." Without the auth module, the platform never injects `X-MS-CLIENT-PRINCIPAL-NAME` / `X-MS-CLIENT-PRINCIPAL-ID` headers. Your app sees an unauthenticated request even though the user is signed in.

**Fix:** Use `requireAuthentication: false` + `unauthenticatedClientAction: 'AllowAnonymous'` and let the app decide per-route. EasyAuth still runs, still injects headers when a cookie is present, and never blocks. The SPA drives sign-in/sign-out explicitly via `/.auth/login/aad` and `/.auth/logout`.

**Where this lives:** `infra/modules/appservice.bicep` — the `auth` and `authStaging` resources.

---

## Tenant policy lock-outs (MS tenant only)

**What it looks like:**
- `az ad app create` errors with `ServiceManagementReference field is required`.
- `azure/login@v2` in GitHub Actions errors with `AADSTS7002381: ... must contain the enterprise claim with value 'microsoft', 'github' or 'microsoftopensource'`.

**Why:** The MS corp tenant has policies that block Entra app registrations and federated identity flows for non-corporate GitHub repos. Personal-account repos (`NicL9923/...`) don't satisfy them.

**Fix:** Deploy in the **App Service UX tenant** (`bb34272d-0432-4e5e-9f0f-e7aca4a450a8`, sub `bce49949-…`). No such policies. This is where MinionTank lives.

**Tell:** If you see those errors and you're targeting the MS tenant, switch subs first.

---

## Cosmos still hard-requires Newtonsoft.Json

**What it looks like:**
```
The Newtonsoft.Json package must be explicitly referenced with version >= 10.0.2.
Please add a reference to Newtonsoft.Json or set the
'AzureCosmosDisableNewtonsoftJsonCheck' property to 'true' to bypass this check.
```

**Why:** `Microsoft.Azure.Cosmos` v3 still pulls in Newtonsoft.Json transitively in 2026. Modern .NET projects often don't have it directly anymore, so the check trips.

**Fix:** Add a direct `<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />` to `dotnet-test-app.csproj`. Already done. Don't remove it.

---

## `WEBSITE_RUN_FROM_PACKAGE` + `webapps-deploy@v3` = 503

**What it looks like:** Deploy succeeds, app starts, returns 503 forever. `eventlog.xml` shows:
```
ZipFS: Failed to copy temp file ... MinionTank.deps.json.temp ... Error: 0x800700b7
IIS AspNetCore Module V2: Unable to locate application dependencies
```

**Why:** `WEBSITE_RUN_FROM_PACKAGE=1` tells App Service the wwwroot is a mounted zip — but `azure/webapps-deploy@v3` does a regular zip-deploy that extracts to `wwwroot`. The two mechanisms collide and `deps.json` ends up unreadable.

**Fix:** Remove `WEBSITE_RUN_FROM_PACKAGE` from app settings. Already done in `infra/modules/appservice.bicep`. Don't re-add unless you switch the deploy action to `azure/webapp-deployments-with-package@*` or similar.

---

## Soft-deleted KV / Cosmos hold global names hostage

**What it looks like:** New deploy fails with:
```
VaultAlreadyExists: ... it is possible that a vault with the same name was recently deleted but not purged
BadRequest: Dns record for cosmos-X under zone Document is already taken
```

**Why:** Both Key Vault and Cosmos have global-namespace name reservations that survive resource group deletion. KV soft-delete keeps the name reserved for `softDeleteRetentionInDays` (we use 7). Cosmos can hold a name for ~minutes-to-hours after delete.

**Fix options:**
- Wait it out (Cosmos: minutes; KV: 7 days).
- Purge: `az keyvault purge -n <name> -l <region>` (only works if you have purge perms and `enablePurgeProtection` is **off**, which we have **on** for safety — so purging is intentionally hard for our prod KV).
- Rename and move on (we did this — original suffix was `-nl`, current is `-aux`).

**Tell:** If you're rebuilding from scratch and hit this, it's almost always a previous incarnation lingering, not a typo.

---

## DefaultAzureCredential picks the wrong tenant locally

**What it looks like:** Cosmos client returns `Unauthorized (401); Substatus: 5007` with a message like `Provided AAD token was issued by the authority [<MS tenant>] which is not trusted by this database account. Please ensure the token has been issued by the AAD tenant(s) [<App Service UX tenant>].`

**Why:** When you're a guest in multiple tenants, `DefaultAzureCredential` picks the home tenant by default — which is the MS tenant for `nicolaslayne@microsoft.com`. Cosmos validates the token's issuer against its own data plane tenant.

**Fix:** Set the env var before running locally:
```powershell
$env:AZURE_TENANT_ID = "bb34272d-0432-4e5e-9f0f-e7aca4a450a8"
dotnet run
```

Or use `az login --tenant bb34272d-…` to pin your CLI session to the App Service UX tenant. Either works because `DefaultAzureCredential` reads `AZURE_TENANT_ID` first, then falls back to `az` CLI.

**On App Service:** Not an issue — managed identity is tenant-pinned by the resource itself.

---

## `dotnet run` picks port 5248 from launchSettings, not your `--urls`

**What it looks like:** You set `ASPNETCORE_URLS=http://localhost:5099`, run, and then can't reach the app on 5099.

**Why:** When `Properties/launchSettings.json` exists, `dotnet run` honors its `applicationUrl` (typically `http://localhost:5248` from the template) over your env var.

**Fix:** Either edit `launchSettings.json` or pass `--urls http://localhost:5099` directly to `dotnet run`. The latter beats both.

---

## Git remote is owned by `NicL9923` but you're often signed in as `nicolaslayne_microsoft`

**What it looks like:** `git push` returns `403 Permission denied` even though `gh auth status` shows you signed in.

**Why:** `gh` lets you have two accounts at once (`nicolaslayne_microsoft` and `NicL9923`) but only one is "active." If the active one isn't the repo owner, push 403s.

**Fix:**
```powershell
gh auth switch --hostname github.com --user NicL9923
gh auth setup-git --hostname github.com   # only needed once per machine
git push
```

The `gh auth setup-git` line writes a credential helper config so subsequent pushes use the active gh token automatically.

---

## Maintenance

When you discover a new gotcha:
1. Add a section here. Match the format: **what it looks like / why / fix**.
2. If the gotcha was caused by something you fixed, link the commit or doc.
3. If the underlying issue is fixed upstream, mark the section `~~struck through~~` rather than deleting — the symptom history is sometimes useful to others Googling.
