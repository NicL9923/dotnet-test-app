namespace MinionTank.Models;

public sealed record Post(
    string id,
    string postId,
    string authorAgentId,
    string body,
    DateTimeOffset createdAt,
    DateTimeOffset? editedAt,
    bool isDeleted,
    Counters counters)
{
    public string? _etag { get; init; }
}

public sealed record Counters(int comments, int likes, int dislikes);

public sealed record Comment(
    string id,
    string postId,
    string? parentCommentId,
    int depth,
    string authorAgentId,
    string body,
    DateTimeOffset createdAt,
    DateTimeOffset? editedAt,
    bool isDeleted);

public sealed record Reaction(
    string id,
    string postId,
    string agentId,
    string kind,
    DateTimeOffset createdAt);

public sealed record Agent(
    string id,
    string agentId,
    string displayName,
    DateTimeOffset createdAt,
    string createdBy,
    string? createdByName,
    string status,
    AgentApiKey apiKey,
    string[] scopes);

public sealed record AgentApiKey(
    string hash,
    string salt,
    string hint,
    string lastFour,
    DateTimeOffset rotatedAt,
    DateTimeOffset expiresAt);

public sealed record CreatePostRequest(string body);
public sealed record CreateCommentRequest(string body, string? parentCommentId);
public sealed record CreateReactionRequest(string kind);
public sealed record CreateAgentRequest(string displayName, string[]? scopes);

public sealed record CreateAgentResponse(
    string agentId,
    string displayName,
    string apiKey,
    DateTimeOffset expiresAt,
    string[] scopes);

public sealed record AgentSummary(
    string agentId,
    string displayName,
    DateTimeOffset createdAt,
    string createdBy,
    string status,
    string lastFour,
    DateTimeOffset rotatedAt,
    DateTimeOffset expiresAt,
    string[] scopes);

public sealed record AuthorSummary(
    string agentId,
    string displayName,
    string ownerFirstName,
    string label);

public sealed record PostFeedItem(
    string postId,
    string authorAgentId,
    AuthorSummary author,
    string body,
    DateTimeOffset createdAt,
    Counters counters);

public sealed record CommentNode(
    string commentId,
    string postId,
    string? parentCommentId,
    int depth,
    string authorAgentId,
    AuthorSummary author,
    string body,
    DateTimeOffset createdAt,
    bool isDeleted);

public sealed record FeedResponse(IReadOnlyList<PostFeedItem> items, string? continuation);
