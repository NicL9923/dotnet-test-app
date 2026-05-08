using System.Reflection;
using System.Runtime.InteropServices;

namespace MinionTank.Endpoints;

public static class HealthInfoEndpoints
{
    public static IEndpointRouteBuilder MapHealth(this IEndpointRouteBuilder app)
    {
        app.MapGet("/healthz", () =>
            TypedResults.Ok(new HealthResponse(
                Status: "Healthy",
                TimeUtc: DateTimeOffset.UtcNow,
                Environment: app.ServiceProvider.GetRequiredService<IHostEnvironment>().EnvironmentName)))
            .ExcludeFromDescription();
        return app;
    }

    public static IEndpointRouteBuilder MapInfo(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/info", (HttpContext httpContext, IHostEnvironment env, IConfiguration config) =>
        {
            httpContext.Response.Headers.CacheControl = "no-store";

            return TypedResults.Ok(new AppInfoResponse(
                ApplicationName: "MinionTank",
                Environment: env.EnvironmentName,
                Framework: RuntimeInformation.FrameworkDescription,
                InformationalVersion: GetInformationalVersion(),
                TimeUtc: DateTimeOffset.UtcNow,
                RequestHost: httpContext.Request.Host.Value,
                CosmosEndpoint: config["Cosmos:Endpoint"],
                CosmosDatabase: config["Cosmos:DatabaseId"],
                AuthDevMode: string.Equals(config["Auth:DevMode"], "true", StringComparison.OrdinalIgnoreCase),
                WebsiteSiteName: GetEnvironmentVariable("WEBSITE_SITE_NAME"),
                WebsiteHostname: GetEnvironmentVariable("WEBSITE_HOSTNAME"),
                WebsiteInstanceId: GetEnvironmentVariable("WEBSITE_INSTANCE_ID"),
                WebsiteResourceGroup: GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP"),
                RegionName: GetEnvironmentVariable("REGION_NAME"),
                SourceVersion: GetEnvironmentVariable("SCM_COMMIT_ID", "WEBSITE_COMMIT_ID", "SOURCE_VERSION"),
                CommitSha: GetEnvironmentVariable("COMMIT_SHA", "GITHUB_SHA")));
        });
        return app;
    }

    private static string? GetEnvironmentVariable(params string[] names)
    {
        foreach (var name in names)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return null;
    }

    private static string? GetInformationalVersion() =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
}

internal sealed record HealthResponse(
    string Status,
    DateTimeOffset TimeUtc,
    string Environment);

internal sealed record AppInfoResponse(
    string ApplicationName,
    string Environment,
    string Framework,
    string? InformationalVersion,
    DateTimeOffset TimeUtc,
    string? RequestHost,
    string? CosmosEndpoint,
    string? CosmosDatabase,
    bool AuthDevMode,
    string? WebsiteSiteName,
    string? WebsiteHostname,
    string? WebsiteInstanceId,
    string? WebsiteResourceGroup,
    string? RegionName,
    string? SourceVersion,
    string? CommitSha);
