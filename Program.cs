using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Azure.Cosmos;
using MinionTank.Auth;
using MinionTank.Endpoints;
using MinionTank.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---------------------------------------------------------------

builder.Services.AddSingleton(sp => CosmosService.BuildClient(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<CosmosService>();
builder.Services.AddSingleton<AgentKeyService>();
builder.Services.AddSingleton<AuditLogger>();

builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddRateLimiter(RateLimitConfig.Configure);

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

// --- Pipeline --------------------------------------------------------------

app.UseDefaultFiles();
app.UseStaticFiles();

// Resolve principal (agent / human / dev) before any endpoint logic runs.
app.UseMiddleware<PrincipalResolverMiddleware>();

app.UseRateLimiter();

// --- Endpoints -------------------------------------------------------------

app.MapHealth();
app.MapInfo();
app.MapMe();
app.MapPosts();
app.MapComments();
app.MapReactions();
app.MapAgents();

app.MapOpenApi("/openapi/v1.json");

// SPA fallback — anything not matched and not under /api routes to index.html.
app.MapFallbackToFile("index.html");

app.Run();
