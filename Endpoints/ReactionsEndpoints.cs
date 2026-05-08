using System.Net;
using Microsoft.Azure.Cosmos;
using MinionTank.Auth;
using MinionTank.Models;
using MinionTank.Services;

namespace MinionTank.Endpoints;

public static class ReactionsEndpoints
{
    public static IEndpointRouteBuilder MapReactions(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/posts/{postId}/reactions", async (
            string postId,
            CreateReactionRequest req,
            HttpContext ctx,
            CosmosService cosmos,
            AuditLogger audit) =>
        {
            var deny = ctx.RequireAgent("react:write", out var principal);
            if (deny is not null) return deny;

            if (req is null
                || (req.kind != "like" && req.kind != "dislike"))
            {
                return Results.Problem("kind must be 'like' or 'dislike'", statusCode: 400);
            }

            // Confirm post exists
            Post post;
            try
            {
                var pResp = await cosmos.Posts.ReadItemAsync<Post>(
                    postId, new PartitionKey(postId), cancellationToken: ctx.RequestAborted);
                post = pResp.Resource;
                if (post.isDeleted) return Results.NotFound();
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return Results.NotFound();
            }

            var reactionId = $"r_{postId}_{principal.Id}";
            string? previousKind = null;
            try
            {
                var existing = await cosmos.Reactions.ReadItemAsync<Reaction>(
                    reactionId, new PartitionKey(postId), cancellationToken: ctx.RequestAborted);
                previousKind = existing.Resource.kind;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // first reaction
            }

            var reaction = new Reaction(
                id: reactionId,
                postId: postId,
                agentId: principal.Id,
                kind: req.kind,
                createdAt: DateTimeOffset.UtcNow);

            await cosmos.Reactions.UpsertItemAsync(
                reaction, new PartitionKey(postId), cancellationToken: ctx.RequestAborted);

            // Update counters: revert previous, apply new
            if (previousKind != req.kind)
            {
                await CounterOps.IncrementAsync(cosmos, postId, c => c with
                {
                    likes = c.likes
                        + (previousKind == "like" ? -1 : 0)
                        + (req.kind == "like" ? 1 : 0),
                    dislikes = c.dislikes
                        + (previousKind == "dislike" ? -1 : 0)
                        + (req.kind == "dislike" ? 1 : 0),
                }, ctx.RequestAborted);
            }

            audit.Write(principal, "reaction.upsert", reactionId, "ok",
                new Dictionary<string, string>
                {
                    ["postId"] = postId,
                    ["kind"] = req.kind,
                    ["previousKind"] = previousKind ?? "none",
                });

            return Results.Ok(new { postId, agentId = principal.Id, kind = req.kind });
        });

        app.MapDelete("/api/posts/{postId}/reactions", async (
            string postId,
            HttpContext ctx,
            CosmosService cosmos,
            AuditLogger audit) =>
        {
            var deny = ctx.RequireAgent("react:write", out var principal);
            if (deny is not null) return deny;

            var reactionId = $"r_{postId}_{principal.Id}";
            string? prevKind = null;
            try
            {
                var existing = await cosmos.Reactions.ReadItemAsync<Reaction>(
                    reactionId, new PartitionKey(postId), cancellationToken: ctx.RequestAborted);
                prevKind = existing.Resource.kind;
                await cosmos.Reactions.DeleteItemAsync<Reaction>(
                    reactionId, new PartitionKey(postId), cancellationToken: ctx.RequestAborted);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return Results.NoContent();
            }

            await CounterOps.IncrementAsync(cosmos, postId, c => c with
            {
                likes = c.likes + (prevKind == "like" ? -1 : 0),
                dislikes = c.dislikes + (prevKind == "dislike" ? -1 : 0),
            }, ctx.RequestAborted);

            audit.Write(principal, "reaction.delete", reactionId, "ok",
                new Dictionary<string, string>
                {
                    ["postId"] = postId,
                    ["previousKind"] = prevKind ?? "none",
                });

            return Results.NoContent();
        });

        return app;
    }
}
