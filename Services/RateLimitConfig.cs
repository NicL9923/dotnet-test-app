using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using MinionTank.Auth;

namespace MinionTank.Services;

/// <summary>
/// Global per-principal token bucket rate limiter.
/// Authenticated principals get 600 requests/minute; anonymous unauth get 30/min by IP.
/// Applied via <c>opts.GlobalLimiter</c> in Program.cs.
/// </summary>
public static class RateLimitConfig
{
    public static void Configure(RateLimiterOptions opts)
    {
        opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        {
            var p = ctx.GetPrincipal();
            if (p.IsAuthenticated)
            {
                return RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: $"{p.Kind}:{p.Id}",
                    factory: _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 600,
                        TokensPerPeriod = 600,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true,
                    });
            }

            return RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: $"ip:{ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 30,
                    TokensPerPeriod = 30,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true,
                });
        });
    }
}
