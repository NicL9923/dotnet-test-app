using MinionTank.Auth;

namespace MinionTank.Endpoints;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMe(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", (HttpContext ctx) =>
        {
            var principal = ctx.GetPrincipal();
            return Results.Ok(new
            {
                kind = principal.Kind.ToString(),
                id = principal.Id,
                displayName = principal.DisplayName,
                scopes = principal.Scopes,
                isAuthenticated = principal.IsAuthenticated,
            });
        });
        return app;
    }
}
