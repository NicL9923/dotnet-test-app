using System.Text.Json;
using MinionTank.Services;

namespace MinionTank.Auth;

public sealed class PrincipalResolverMiddleware
{
    private const string AgentKeyHeader = "X-Agent-Key";
    private const string EasyAuthNameHeader = "X-MS-CLIENT-PRINCIPAL-NAME";
    private const string EasyAuthIdHeader = "X-MS-CLIENT-PRINCIPAL-ID";
    private const string EasyAuthPrincipalHeader = "X-MS-CLIENT-PRINCIPAL";

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
                agent.agentId,
                agent.displayName,
                agent.scopes));
        }
        else if (ctx.Request.Headers.TryGetValue(EasyAuthNameHeader, out var upn)
            && !string.IsNullOrWhiteSpace(upn))
        {
            var oid = ctx.Request.Headers[EasyAuthIdHeader].ToString();
            var friendlyName = ExtractFriendlyName(ctx.Request.Headers[EasyAuthPrincipalHeader].ToString())
                ?? upn.ToString();
            ctx.SetPrincipal(new Principal(
                PrincipalKind.Human,
                string.IsNullOrEmpty(oid) ? upn.ToString() : oid,
                upn.ToString(),
                friendlyName,
                []));
        }
        else if (_devMode)
        {
            ctx.SetPrincipal(new Principal(
                PrincipalKind.Dev,
                "dev-user@local",
                "dev-user@local",
                "Local Dev",
                []));
        }

        await _next(ctx);
    }

    private string? ExtractFriendlyName(string base64Principal)
    {
        if (string.IsNullOrWhiteSpace(base64Principal))
        {
            return null;
        }

        try
        {
            var json = Convert.FromBase64String(base64Principal);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("claims", out var claims) || claims.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            string? name = null;
            string? givenName = null;
            foreach (var claim in claims.EnumerateArray())
            {
                if (!claim.TryGetProperty("typ", out var typ) || !claim.TryGetProperty("val", out var val))
                {
                    continue;
                }
                var t = typ.GetString();
                var v = val.GetString();
                if (string.IsNullOrWhiteSpace(v))
                {
                    continue;
                }
                if (t is "name" or "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")
                {
                    name = v;
                }
                else if (t is "given_name" or "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname")
                {
                    givenName = v;
                }
            }

            return name ?? givenName;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            _logger.LogDebug(ex, "Failed to parse X-MS-CLIENT-PRINCIPAL payload.");
            return null;
        }
    }
}
