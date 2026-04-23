using System.Reflection;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/healthz", () =>
    TypedResults.Ok(
        new HealthResponse(
            Status: "Healthy",
            TimeUtc: DateTimeOffset.UtcNow,
            Environment: app.Environment.EnvironmentName)));

app.MapGet("/api/info", (HttpContext httpContext) =>
{
    httpContext.Response.Headers.CacheControl = "no-store";

    return TypedResults.Ok(
        new AppInfoResponse(
            ApplicationName: app.Environment.ApplicationName,
            Environment: app.Environment.EnvironmentName,
            Framework: RuntimeInformation.FrameworkDescription,
            InformationalVersion: GetInformationalVersion(),
            TimeUtc: DateTimeOffset.UtcNow,
            RequestHost: httpContext.Request.Host.Value,
            WebsiteSiteName: GetEnvironmentVariable("WEBSITE_SITE_NAME"),
            WebsiteHostname: GetEnvironmentVariable("WEBSITE_HOSTNAME"),
            WebsiteInstanceId: GetEnvironmentVariable("WEBSITE_INSTANCE_ID"),
            WebsiteResourceGroup: GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP"),
            RegionName: GetEnvironmentVariable("REGION_NAME"),
            SourceVersion: GetEnvironmentVariable("SCM_COMMIT_ID", "WEBSITE_COMMIT_ID", "SOURCE_VERSION"),
            CommitSha: GetEnvironmentVariable("COMMIT_SHA", "GITHUB_SHA")));
});

app.Run();

static string? GetEnvironmentVariable(params string[] names)
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

static string? GetInformationalVersion()
{
    return Assembly
        .GetExecutingAssembly()
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
    string? WebsiteSiteName,
    string? WebsiteHostname,
    string? WebsiteInstanceId,
    string? WebsiteResourceGroup,
    string? RegionName,
    string? SourceVersion,
    string? CommitSha);
