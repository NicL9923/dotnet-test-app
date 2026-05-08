using System.Net;
using Microsoft.Azure.Cosmos;
using MinionTank.Auth;
using MinionTank.Models;
using MinionTank.Services;

namespace MinionTank.Endpoints;

public static class PostsEndpoints
{
    private const int BodyMaxLength = 4000;

    public static IEndpointRouteBuilder MapPosts(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/posts");

        grp.MapGet("/", async (
            HttpContext ctx,
            CosmosService cosmos,
            string? continuation,
            int? limit) =>
        {
            var top = Math.Clamp(limit ?? 20, 1, 50);
            var query = new QueryDefinition(
                "SELECT TOP @top * FROM c WHERE c.isDeleted = false ORDER BY c.createdAt DESC")
                .WithParameter("@top", top);

            using var iter = cosmos.Posts.GetItemQueryIterator<Post>(
                query,
                continuationToken: continuation,
                requestOptions: new QueryRequestOptions { MaxItemCount = top });

            var items = new List<PostFeedItem>();
            string? next = null;
            if (iter.HasMoreResults)
            {
                var page = await iter.ReadNextAsync(ctx.RequestAborted);
                next = page.ContinuationToken;
                foreach (var p in page)
                {
                    items.Add(new PostFeedItem(
                        p.postId, p.authorAgentId, p.body, p.createdAt, p.counters));
                }
            }

            return Results.Ok(new FeedResponse(items, next));
        });

        grp.MapGet("/{postId}", async (
            string postId,
            HttpContext ctx,
            CosmosService cosmos) =>
        {
            try
            {
                var resp = await cosmos.Posts.ReadItemAsync<Post>(
                    postId, new PartitionKey(postId), cancellationToken: ctx.RequestAborted);
                if (resp.Resource.isDeleted)
                {
                    return Results.NotFound();
                }
                return Results.Ok(new PostFeedItem(
                    resp.Resource.postId,
                    resp.Resource.authorAgentId,
                    resp.Resource.body,
                    resp.Resource.createdAt,
                    resp.Resource.counters));
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return Results.NotFound();
            }
        });

        grp.MapPost("/", async (
            HttpContext ctx,
            CreatePostRequest req,
            CosmosService cosmos,
            AuditLogger audit) =>
        {
            var deny = ctx.RequireAgent("post:write", out var principal);
            if (deny is not null) return deny;

            if (req is null || string.IsNullOrWhiteSpace(req.body))
            {
                return Results.Problem("body must not be empty", statusCode: 400);
            }
            if (req.body.Length > BodyMaxLength)
            {
                return Results.Problem(
                    $"body exceeds {BodyMaxLength} chars", statusCode: 400);
            }

            var postId = $"p_{Guid.CreateVersion7():N}";
            var post = new Post(
                id: postId,
                postId: postId,
                authorAgentId: principal.Id,  // server-stamped (Moltbook F5 countermeasure)
                body: req.body.Trim(),
                createdAt: DateTimeOffset.UtcNow,
                editedAt: null,
                isDeleted: false,
                counters: new Counters(0, 0, 0));

            await cosmos.Posts.CreateItemAsync(
                post, new PartitionKey(postId), cancellationToken: ctx.RequestAborted);

            audit.Write(principal, "post.create", postId, "ok");

            return Results.Created($"/api/posts/{postId}", new PostFeedItem(
                post.postId, post.authorAgentId, post.body, post.createdAt, post.counters));
        });

        return app;
    }
}
