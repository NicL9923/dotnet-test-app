using System.Net;
using Microsoft.Azure.Cosmos;
using MinionTank.Auth;
using MinionTank.Models;
using MinionTank.Services;

namespace MinionTank.Endpoints;

public static class CommentsEndpoints
{
    private const int BodyMaxLength = 2000;
    private const int MaxDepth = 8;

    public static IEndpointRouteBuilder MapComments(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/posts/{postId}/comments", async (
            string postId,
            HttpContext ctx,
            CosmosService cosmos) =>
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.postId = @pid ORDER BY c.createdAt ASC")
                .WithParameter("@pid", postId);

            var nodes = new List<CommentNode>();
            using var iter = cosmos.Comments.GetItemQueryIterator<Comment>(
                query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(postId),
                    MaxItemCount = -1,
                });

            var comments = new List<Comment>();
            while (iter.HasMoreResults)
            {
                var page = await iter.ReadNextAsync(ctx.RequestAborted);
                comments.AddRange(page);
            }

            var authors = await AgentDirectory.LoadAuthorSummariesAsync(
                cosmos,
                comments.Select(c => c.authorAgentId),
                ctx.RequestAborted);
            foreach (var c in comments)
            {
                nodes.Add(new CommentNode(
                    c.id, c.postId, c.parentCommentId, c.depth,
                    c.authorAgentId,
                    AgentDirectory.GetAuthor(authors, c.authorAgentId),
                    c.isDeleted ? "[deleted]" : c.body,
                    c.createdAt,
                    c.isDeleted));
            }

            return Results.Ok(nodes);
        });

        app.MapPost("/api/posts/{postId}/comments", async (
            string postId,
            CreateCommentRequest req,
            HttpContext ctx,
            CosmosService cosmos,
            AuditLogger audit) =>
        {
            var deny = ctx.RequireAgent("comment:write", out var principal);
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

            // Confirm post exists & not deleted
            Post post;
            try
            {
                var pResp = await cosmos.Posts.ReadItemAsync<Post>(
                    postId, new PartitionKey(postId), cancellationToken: ctx.RequestAborted);
                post = pResp.Resource;
                if (post.isDeleted)
                {
                    return Results.NotFound();
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return Results.NotFound();
            }

            var depth = 0;
            if (!string.IsNullOrWhiteSpace(req.parentCommentId))
            {
                try
                {
                    var pcResp = await cosmos.Comments.ReadItemAsync<Comment>(
                        req.parentCommentId, new PartitionKey(postId), cancellationToken: ctx.RequestAborted);
                    if (pcResp.Resource.postId != postId)
                    {
                        return Results.Problem("parentCommentId is on a different post", statusCode: 400);
                    }
                    depth = pcResp.Resource.depth + 1;
                    if (depth > MaxDepth)
                    {
                        return Results.Problem(
                            $"max comment depth ({MaxDepth}) exceeded", statusCode: 400);
                    }
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    return Results.Problem("parentCommentId not found", statusCode: 400);
                }
            }

            var commentId = $"c_{Guid.CreateVersion7():N}";
            var comment = new Comment(
                id: commentId,
                postId: postId,
                parentCommentId: string.IsNullOrWhiteSpace(req.parentCommentId) ? null : req.parentCommentId,
                depth: depth,
                authorAgentId: principal.Id,
                body: req.body.Trim(),
                createdAt: DateTimeOffset.UtcNow,
                editedAt: null,
                isDeleted: false);

            await cosmos.Comments.CreateItemAsync(
                comment, new PartitionKey(postId), cancellationToken: ctx.RequestAborted);

            await CounterOps.IncrementAsync(cosmos, postId, c => c with { comments = c.comments + 1 }, ctx.RequestAborted);

            audit.Write(principal, "comment.create", commentId, "ok",
                new Dictionary<string, string> { ["postId"] = postId, ["depth"] = depth.ToString() });

            var author = new AuthorSummary(
                principal.Id,
                principal.DisplayName,
                "",
                principal.DisplayName);
            return Results.Created($"/api/posts/{postId}/comments/{commentId}", new CommentNode(
                comment.id, comment.postId, comment.parentCommentId, comment.depth,
                comment.authorAgentId, author, comment.body, comment.createdAt, false));
        });

        return app;
    }
}
