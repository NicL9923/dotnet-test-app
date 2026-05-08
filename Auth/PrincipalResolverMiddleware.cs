using MinionTank.Services;

namespace MinionTank.Auth;

public sealed class PrincipalResolverMiddleware
{
    private const string AgentKeyHeader = "X-Agent-Key";
    private const string EasyAuthNameHeader = "X-MS-CLIENT-PRINCIPAL-NAME";
    private const string EasyAuthIdHeader = "X-MS-CLIENT-PRINCIPAL-ID";

    private readonly RequestDelegate _next;
    private readonly AgentKeyService _agentKeys;
    private readonly bool _devMode;
    private readonly ILogger<PrincipalResolverMiddleware> _logger;

    public PrincipalResolverMiddleware(
        RequestDelegate next,
        AgentKeyService agentKeys,
        IConfiguration config,
        ILogger<PrincipalResolverMiddleware> logger)
    {
        _next = next;
        _agentKeys = agentKeys;
        _logger = logger;
        _devMode = string.Equals(
            config["Auth:DevMode"],
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.Request.Headers.TryGetValue(AgentKeyHeader, out var agentKey)
            && !string.IsNullOrWhiteSpace(agentKey))
        {
            var agent = await _agentKeys.ValidateAsync(agentKey.ToString(), ctx.RequestAborted);
            if (agent is null)
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                ctx.Response.ContentType = "application/problem+json";
                await ctx.Response.WriteAsync(
                    """{"type":"https://api.miniontank/errors/agent-key-invalid","title":"Invalid agent key","status":401,"detail":"The agent key was not recognized, is revoked, or has expired."}""",
                    ctx.RequestAborted);
                return;
            }

            ctx.SetPrincipal(new Principal(
                PrincipalKind.Agent,
                agent.agentId,
                agent.displayName,
                agent.scopes));
        }
        else if (ctx.Request.Headers.TryGetValue(EasyAuthNameHeader, out var upn)
            && !string.IsNullOrWhiteSpace(upn))
        {
            var oid = ctx.Request.Headers[EasyAuthIdHeader].ToString();
            ctx.SetPrincipal(new Principal(
                PrincipalKind.Human,
                string.IsNullOrEmpty(oid) ? upn.ToString() : oid,
                upn.ToString(),
                []));
        }
        else if (_devMode)
        {
            ctx.SetPrincipal(new Principal(
                PrincipalKind.Dev,
                "dev-user@local",
                "Local Dev",
                []));
        }

        await _next(ctx);
    }
}
