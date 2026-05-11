namespace MinionTank.Auth;

public enum PrincipalKind
{
    None,
    Agent,
    Human,
    Dev,
}

public sealed record Principal(
    PrincipalKind Kind,
    string Id,
    string Upn,
    string DisplayName,
    string[] Scopes)
{
    public static Principal None { get; } = new(PrincipalKind.None, "", "", "", []);

    public bool IsAuthenticated => Kind != PrincipalKind.None;
    public bool IsAgent => Kind == PrincipalKind.Agent;
    public bool IsHuman => Kind == PrincipalKind.Human;
    public bool IsDev => Kind == PrincipalKind.Dev;
    public bool HasScope(string scope) => Scopes.Contains(scope);
}

public static class PrincipalAccessor
{
    private const string Key = "MinionTank.Principal";

    public static Principal GetPrincipal(this HttpContext ctx) =>
        ctx.Items.TryGetValue(Key, out var p) && p is Principal principal
            ? principal
            : Principal.None;

    public static void SetPrincipal(this HttpContext ctx, Principal principal) =>
        ctx.Items[Key] = principal;

    public static IResult RequireAgent(this HttpContext ctx, string scope, out Principal principal)
    {
        principal = ctx.GetPrincipal();
        if (!principal.IsAgent)
        {
            return Results.Problem(
                title: "Agent authentication required",
                statusCode: StatusCodes.Status401Unauthorized,
                type: "https://api.miniontank/errors/agent-required");
        }
        if (!principal.HasScope(scope))
        {
            return Results.Problem(
                title: $"Missing scope: {scope}",
                statusCode: StatusCodes.Status403Forbidden,
                type: "https://api.miniontank/errors/scope-missing");
        }
        return null!;
    }

    public static IResult RequireHuman(this HttpContext ctx, out Principal principal)
    {
        principal = ctx.GetPrincipal();
        if (!principal.IsHuman && !principal.IsDev)
        {
            return Results.Problem(
                title: "Human authentication required",
                statusCode: StatusCodes.Status401Unauthorized,
                type: "https://api.miniontank/errors/human-required");
        }
        return null!;
    }
}
