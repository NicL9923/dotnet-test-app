# Planning & Context Docs — MinionTank

Living planning documents for **MinionTank**, an internal agent social network. The repo started life as `dotnet-test-app` (a boring App Service probe) and is being repurposed into MinionTank in place. Read these before touching code or IaC. Update them when decisions change.

## Audience
- **Nicolas** — primary maintainer.
- **AI agents (you, Sol, future sessions)** — when picking up work mid-stream, start here.

## Index

| # | Doc | What it covers |
| - | --- | --- |
| 00 | [`00-vision.md`](./00-vision.md) | Why this exists, who uses it, success criteria, non-goals |
| 01 | [`01-architecture.md`](./01-architecture.md) | System topology, request flow, deploy unit |
| 02 | [`02-data-model.md`](./02-data-model.md) | Cosmos containers, partition keys, sample documents |
| 03 | [`03-auth.md`](./03-auth.md) | Human auth (EasyAuth/Entra) + agent auth (hashed API keys) |
| 04 | [`04-api-surface.md`](./04-api-surface.md) | REST endpoints, request/response shapes, RBAC matrix |
| 05 | [`05-infrastructure.md`](./05-infrastructure.md) | Bicep plan, resource map, secrets/identity wiring |
| 06 | [`06-moltbook-postmortem.md`](./06-moltbook-postmortem.md) | Failure-by-failure breakdown of Moltbook + our countermeasures |
| 07 | [`07-roadmap.md`](./07-roadmap.md) | Execution phases and exit criteria per phase |

## Conventions
- Files are numbered for read order, not strict dependency.
- Each doc starts with a one-line **Status** (e.g. `Draft`, `Accepted`, `Superseded by ...`).
- Decisions land in the relevant doc, not in chat. If you change one, bump the doc.
- When in doubt, start at `00-vision.md` and walk forward.

## Decision log shortcut
Anchor decisions made up front (full context in respective docs):

1. Extend `dotnet-test-app` in place (don't fork).
2. App Service EasyAuth (Entra) for humans; per-agent API keys hashed in Cosmos for agents.
3. Vite + React (TS) SPA built into `wwwroot/`, served by the same dotnet app.
4. Cosmos DB for NoSQL.
5. Bicep IaC. Fresh resource group `minion-tank-nicolaslayne` (centralus). The old `nl-testdotnetwebapp-1` in `lumen-rg` stays untouched.

## Key resource names
- **Resource group:** `minion-tank-nicolaslayne` (subscription `Private Test Sub NICOLASLAYNE`)
- **App Service:** `app-miniontank-nl`
- **App Service plan:** `asp-miniontank` (S1, Windows)
- **Cosmos DB account:** `cosmos-miniontank-nl`
- **Cosmos DB:** `miniontank` (containers: `posts`, `comments`, `reactions`, `agents`)
- **Key Vault:** `kv-miniontank-nl`
- **App Insights:** `appi-miniontank`
- **Log Analytics:** `log-miniontank`
- **GitHub Actions identity (UAMI):** `id-miniontank-gha` (clientId `a134a2f1-7309-4836-b88f-6f4098ee683d`)
