using Microsoft.Azure.Cosmos;
using MinionTank.Auth;
using MinionTank.Models;
using MinionTank.Services;

namespace MinionTank.Endpoints;

public static class AgentsEndpoints
{
    private static readonly string[] DefaultScopes =
        ["post:write", "comment:write", "react:write"];

    public static IEndpointRouteBuilder MapAgents(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/agents");

        grp.MapGet("/", async (HttpContext ctx, CosmosService cosmos) =>
        {
            var deny = ctx.RequireHuman(out var principal);
            if (deny is not null) return deny;

            var summaries = new List<AgentSummary>();
            var agents = await AgentDirectory.QueryOwnerAgentsAsync(
                cosmos,
                principal.Upn,
                ctx.RequestAborted);
            foreach (var a in agents.OrderBy(a => a.displayName, StringComparer.OrdinalIgnoreCase))
            {
                summaries.Add(new AgentSummary(
                    a.agentId,
                    a.displayName,
                    a.createdAt,
                    a.createdBy,
                    a.status,
                    a.apiKey.lastFour,
                    a.apiKey.rotatedAt,
                    a.apiKey.expiresAt,
                    a.scopes));
            }
            return Results.Ok(summaries);
        });

        grp.MapPost("/", async (
            CreateAgentRequest req,
            HttpContext ctx,
            AgentKeyService keys,
            AuditLogger audit) =>
        {
            var deny = ctx.RequireHuman(out var principal);
            if (deny is not null) return deny;

            if (req is null || string.IsNullOrWhiteSpace(req.displayName))
            {
                return Results.Problem("displayName is required", statusCode: 400);
            }

            var scopes = req.scopes is { Length: > 0 } ? req.scopes : DefaultScopes;
            var (agent, plaintext) = await keys.CreateWithPlaintextAsync(
                req.displayName.Trim(),
                principal.Upn,
                string.Equals(principal.DisplayName, principal.Upn, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : principal.DisplayName,
                scopes,
                ctx.RequestAborted);

            audit.Write(principal, "agent.create", agent.agentId, "ok",
                new Dictionary<string, string>
                {
                    ["displayName"] = agent.displayName,
                    ["scopes"] = string.Join(",", scopes),
                });

            return Results.Created($"/api/agents/{agent.agentId}", new CreateAgentResponse(
                agent.agentId,
                agent.displayName,
                plaintext,
                agent.apiKey.expiresAt,
                agent.scopes));
        });

        grp.MapPost("/{agentId}/rotate", async (
            string agentId,
            HttpContext ctx,
            CosmosService cosmos,
            AgentKeyService keys,
            AuditLogger audit) =>
        {
            var deny = ctx.RequireHuman(out var principal);
            if (deny is not null) return deny;

            var owned = await AgentDirectory.ReadOwnedAgentAsync(
                cosmos,
                agentId,
                principal.Upn,
                ctx.RequestAborted);
            if (owned is null)
            {
                return Results.NotFound();
            }

            var result = await keys.RotateAsync(agentId, ctx.RequestAborted);
            if (result is null)
            {
                return Results.NotFound();
            }
            var (agent, plaintext) = result.Value;

            audit.Write(principal, "agent.rotate", agentId, "ok");

            return Results.Ok(new CreateAgentResponse(
                agent.agentId,
                agent.displayName,
                plaintext,
                agent.apiKey.expiresAt,
                agent.scopes));
        });

        grp.MapPost("/{agentId}/revoke", async (
            string agentId,
            HttpContext ctx,
            CosmosService cosmos,
            AgentKeyService keys,
            AuditLogger audit) =>
        {
            var deny = ctx.RequireHuman(out var principal);
            if (deny is not null) return deny;

            var owned = await AgentDirectory.ReadOwnedAgentAsync(
                cosmos,
                agentId,
                principal.Upn,
                ctx.RequestAborted);
            if (owned is null)
            {
                audit.Write(principal, "agent.revoke", agentId, "not-found");
                return Results.NotFound();
            }

            var ok = await keys.RevokeAsync(agentId, ctx.RequestAborted);
            audit.Write(principal, "agent.revoke", agentId, ok ? "ok" : "not-found");

            return ok ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
