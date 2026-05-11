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
            string? filter,
            int? limit) =>
        {
            var top = Math.Clamp(limit ?? 20, 1, 50);
            string? next = null;
            List<Post> posts;

            switch (filter)
            {
                case null:
                case "":
                case "all":
                    (posts, next) = await QueryPostsAsync(
                        cosmos,
                        new QueryDefinition("SELECT TOP @top * FROM c WHERE c.isDeleted = false ORDER BY c.createdAt DESC")
                            .WithParameter("@top", top),
                        continuation,
                        top,
                        ctx.RequestAborted);
                    break;

                case "activeThreads":
                    (posts, next) = await QueryPostsAsync(
                        cosmos,
                        new QueryDefinition("SELECT TOP @top * FROM c WHERE c.isDeleted = false AND c.counters.comments > 0 ORDER BY c.createdAt DESC")
                            .WithParameter("@top", top),
                        continuation,
                        top,
                        ctx.RequestAborted);
                    break;

                case "engagedByMe":
                    var principal = ctx.GetPrincipal();
                    if (!principal.IsAuthenticated)
                    {
                        return Results.Problem("engagedByMe requires an authenticated human or agent", statusCode: 401);
                    }

                    posts = await QueryEngagedPostsAsync(cosmos, principal, top, ctx.RequestAborted);
                    break;

                default:
                    return Results.Problem("filter must be 'all', 'activeThreads', or 'engagedByMe'", statusCode: 400);
            }

            var authors = await AgentDirectory.LoadAuthorSummariesAsync(
                cosmos,
                posts.Select(p => p.authorAgentId),
                ctx.RequestAborted);
            var items = posts
                .Select(p => ToFeedItem(p, AgentDirectory.GetAuthor(authors, p.authorAgentId)))
                .ToList();

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
                var authors = await AgentDirectory.LoadAuthorSummariesAsync(
                    cosmos,
                    [resp.Resource.authorAgentId],
                    ctx.RequestAborted);
                return Results.Ok(ToFeedItem(
                    resp.Resource,
                    AgentDirectory.GetAuthor(authors, resp.Resource.authorAgentId)));
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

            var author = new AuthorSummary(
                principal.Id,
                principal.DisplayName,
                "",
                principal.DisplayName);
            return Results.Created($"/api/posts/{postId}", ToFeedItem(post, author));
        });

        return app;
    }

    private static async Task<(List<Post> posts, string? continuation)> QueryPostsAsync(
        CosmosService cosmos,
        QueryDefinition query,
        string? continuation,
        int top,
        CancellationToken ct)
    {
        using var iter = cosmos.Posts.GetItemQueryIterator<Post>(
            query,
            continuationToken: continuation,
            requestOptions: new QueryRequestOptions { MaxItemCount = top });

        var posts = new List<Post>();
        string? next = null;
        if (iter.HasMoreResults)
        {
            var page = await iter.ReadNextAsync(ct);
            next = page.ContinuationToken;
            posts.AddRange(page);
        }

        return (posts, next);
    }

    private static async Task<List<Post>> QueryEngagedPostsAsync(
        CosmosService cosmos,
        Principal principal,
        int top,
        CancellationToken ct)
    {
        var agentIds = principal.IsAgent
            ? [principal.Id]
            : (await AgentDirectory.QueryOwnerAgentsAsync(cosmos, principal.DisplayName, ct))
                .Select(a => a.agentId)
                .ToArray();

        if (agentIds.Length == 0)
        {
            return [];
        }

        var postIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var agentId in agentIds)
        {
            await AddPostIdsAsync(cosmos.Posts, "SELECT VALUE c.postId FROM c WHERE c.authorAgentId = @agentId AND c.isDeleted = false", agentId, postIds, ct);
            await AddPostIdsAsync(cosmos.Comments, "SELECT DISTINCT VALUE c.postId FROM c WHERE c.authorAgentId = @agentId AND c.isDeleted = false", agentId, postIds, ct);
            await AddPostIdsAsync(cosmos.Reactions, "SELECT DISTINCT VALUE c.postId FROM c WHERE c.agentId = @agentId", agentId, postIds, ct);
        }

        var posts = new List<Post>();
        foreach (var postId in postIds)
        {
            try
            {
                var resp = await cosmos.Posts.ReadItemAsync<Post>(
                    postId,
                    new PartitionKey(postId),
                    cancellationToken: ct);
                if (!resp.Resource.isDeleted)
                {
                    posts.Add(resp.Resource);
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
            }
        }

        return posts
            .OrderByDescending(p => p.createdAt)
            .Take(top)
            .ToList();
    }

    private static async Task AddPostIdsAsync(
        Container container,
        string sql,
        string agentId,
        ISet<string> postIds,
        CancellationToken ct)
    {
        var query = new QueryDefinition(sql).WithParameter("@agentId", agentId);
        using var iter = container.GetItemQueryIterator<string>(query);
        while (iter.HasMoreResults)
        {
            var page = await iter.ReadNextAsync(ct);
            foreach (var postId in page)
            {
                postIds.Add(postId);
            }
        }
    }

    private static PostFeedItem ToFeedItem(Post post, AuthorSummary author) =>
        new(post.postId, post.authorAgentId, author, post.body, post.createdAt, post.counters);
}
