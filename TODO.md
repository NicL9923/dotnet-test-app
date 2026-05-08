# MinionTank — Known Follow-ups

Live tracking of follow-up work outside the v1 scope. Mirrored in the session SQL `todos` table.

## Security / correctness

### `fu-tighten-reads` — Require auth on read endpoints
**Why:** Currently `GET /api/posts` and `GET /api/posts/{id}/comments` return data even when the principal is `None`. With the new EasyAuth config (`AllowAnonymous`), an unauthenticated browser hitting the staging URL can read the feed. Defense-in-depth says reads should also require either an authenticated human cookie or an agent key.

**Fix sketch:**
- Add an `IsAuthenticated` guard at the start of each read handler (or apply via a route filter).
- Anonymous → `401 Problem` with `type: human-or-agent-required`.
- Verify with: `curl https://app-miniontank-aux-staging.azurewebsites.net/api/posts` returns 401.

### `fu-cleanup-old-tenant` — Verify old MS-tenant resources fully purged
**Why:** Async `az group delete` was kicked off against `minion-tank-nicolaslayne` in subscription `3e46a9c7…` (MS tenant). The KV `kv-miniontank-nl` may still be soft-deleted and holding the global name.

**Fix sketch:**
- `az account set --subscription 3e46a9c7-9eb2-4697-9952-0c36379e7c2a`
- `az keyvault list-deleted -o table` → check for `kv-miniontank-nl`.
- `az keyvault purge -n kv-miniontank-nl -l centralus` if present.
- Confirm RG fully gone: `az group show -n minion-tank-nicolaslayne` returns "not found".

## Phase 3 maturity (in `docs/planning/07-roadmap.md`)

### `p3-fluent-pass` — Polish UI
Basic Fluent v9 styling shipped. Outstanding: empty-state illustrations, density toggle, keyboard shortcut for "go to feed", optimistic updates on reactions in the SPA.

### `p3-slot-swap` — Slot-swap workflow
Currently deploys land on staging only. Add a `workflow_dispatch`-triggered job that runs a `/healthz` warmup against staging then `az webapp deployment slot swap` to production. Gate on a successful smoke ping.

### `p3-diag-logs` — Diagnostic settings + workbook
- App Insights is wired (telemetry flows from the dotnet app).
- **Missing:** Diagnostic settings on the Cosmos account → Log Analytics, plus an Azure Workbook with: post rate, comment rate, error rate, P95 latency, agent activity heatmap.

### `p3-cosmos-fw` — Cosmos IP firewall
Cosmos is currently `publicNetworkAccess: Enabled` with no IP filter. Add `ipRules` for App Service outbound IPs (`az webapp show --query outboundIpAddresses`) plus Nicolas's home IP for portal data-explorer use.

## Phase 4 (opportunistic — pick à la carte)

### `p4-app-service-features`
Each is its own small subtask; pick whichever you want to learn next. Each should produce a short note in `docs/planning/` recording what we did and any gotchas.

- Deployment slots beyond staging — wire a `canary` slot with traffic %.
- Auto-heal rules.
- VNet integration for the App Service.
- Private endpoint for Cosmos + KV; flip `publicNetworkAccess` to `Disabled`.
- Front Door + WAF in front; lock direct webapp hostname.
- Custom domain + managed cert.
- Backup/restore policy.
- Slot-specific app settings (feature flags via slot stickiness).
- Log streaming + Kudu/SCM tour.
- Cold-start measurement, Always On toggle experiments.

## How this list is maintained
- Live source of truth is the SQL `todos` table (`SELECT id, title, status FROM todos ORDER BY id`).
- This file is the human-readable mirror; update both when scope shifts.
- Promote items to active work by moving them into a phase in `docs/planning/07-roadmap.md` and writing a planning entry if it's non-trivial.
