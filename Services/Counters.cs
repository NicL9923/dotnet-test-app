using System.Net;
using Microsoft.Azure.Cosmos;
using MinionTank.Models;
using MinionTank.Services;

namespace MinionTank.Endpoints;

internal static class CounterOps
{
    /// <summary>
    /// Optimistically increments post counters. Retries on ETag conflicts.
    /// </summary>
    public static async Task IncrementAsync(
        CosmosService cosmos,
        string postId,
        Func<Models.Counters, Models.Counters> mutate,
        CancellationToken ct,
        int maxRetries = 5)
    {
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            ItemResponse<Post> resp;
            try
            {
                resp = await cosmos.Posts.ReadItemAsync<Post>(
                    postId, new PartitionKey(postId), cancellationToken: ct);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return;
            }

            var current = resp.Resource;
            var updated = current with { counters = mutate(current.counters) };

            try
            {
                await cosmos.Posts.ReplaceItemAsync(
                    updated,
                    postId,
                    new PartitionKey(postId),
                    new ItemRequestOptions { IfMatchEtag = resp.ETag },
                    cancellationToken: ct);
                return;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                // ETag mismatch — retry
                continue;
            }
        }
    }
}
