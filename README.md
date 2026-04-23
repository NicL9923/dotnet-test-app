# dotnet-test-app

Minimal ASP.NET Core test app for Azure App Service. Right now its main job is to be an easy deployment target, especially for GitHub-based deployment flows. Over time it can grow into a low-complexity probe app for validating more App Service features without changing stacks or dragging in unnecessary dependencies.

## Current scope

- Static landing page at `/`
- Health probe endpoint at `/healthz`
- Runtime and deployment metadata endpoint at `/api/info`
- No database, auth, frontend build step, or background workers

## Why this exists

This repo is meant to stay intentionally boring:

- Fast to build and publish
- Easy to reason about when a deployment goes sideways
- Simple enough that App Service behavior is the thing under test, not the app itself

## Run locally

```powershell
dotnet restore
dotnet run
```

Default local URL:

- `http://localhost:5000` or the URL printed by `dotnet run`

## Endpoints

| Path | Purpose |
| --- | --- |
| `/` | Static landing page with live probe results and roadmap summary |
| `/healthz` | Simple health endpoint for App Service health check configuration |
| `/api/info` | JSON payload with runtime and deployment metadata useful for smoke tests |

## Deployment Center / GitHub Actions notes

For App Service Deployment Center testing, the recommended path is to let Azure generate the deployment workflow in this repo. This repo intentionally includes a CI workflow but does **not** pre-seed an App Service deployment workflow, so Deployment Center can create one without needing cleanup first.

If you want to surface commit metadata in the running app later, stamp `COMMIT_SHA` during your deploy workflow or publish with an `InformationalVersion` tied to the Git SHA.

## Long-term goal

The end-state is a single .NET App Service test app that can light up most platform features by adding focused, low-risk probes instead of product-like complexity.

### Phase 1: Deployment validation

This repo starts here.

- Static site content for easy visual confirmation
- Health endpoint for readiness checks
- Runtime metadata for post-deploy smoke tests
- Compatible with GitHub-based deployment flows

### Phase 2: Configuration and runtime behavior

Add lightweight probes for:

- App settings and slot setting verification
- Connection string presence checks
- Startup command / runtime configuration validation
- Safe allow-listed environment inspection

### Phase 3: Identity and secure dependencies

Add narrow scenarios for:

- Managed identity token acquisition
- Key Vault reference validation
- Auth challenge / authenticated route testing
- Outbound dependency calls with visible success/failure states

### Phase 4: Networking and storage

Add optional probes for:

- VNet-dependent outbound calls
- Private endpoint reachability checks
- Mounted storage validation
- Blob or queue connectivity via managed identity

### Phase 5: Operations and diagnostics

Add operational surfaces for:

- Structured log emission
- Intentional warning/error generation
- Slow-response endpoint for timeout testing
- Slot swap and warmup validation hooks

## Suggested rules for future expansion

- Keep every new feature probe isolated behind a dedicated endpoint or page section.
- Prefer read-only validation over stateful demo logic.
- Avoid adding dependencies unless they unlock a specific App Service feature test.
- Keep the landing page understandable enough to use as a post-deploy smoke test.
