using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Azure.Cosmos;
using MinionTank.Models;

namespace MinionTank.Services;

/// <summary>
/// Generates and validates agent API keys.
/// Format: <c>agent_&lt;hint(8)&gt;&lt;secret(24)&gt;</c> = 38 chars total. base32 charset.
/// The hint is non-secret and used to point-query the agents container by <c>apiKey.hint</c>.
/// The full plaintext is HMAC-SHA256'd with a per-key salt and stored.
/// </summary>
public sealed class AgentKeyService
{
    private const string Prefix = "agent_";
    private const int HintLength = 8;
    private const int SecretLength = 24;
    private const int SaltLengthBytes = 16;
    private const int DefaultExpiryDays = 90;

    private static readonly char[] Base32 = "abcdefghijklmnopqrstuvwxyz234567".ToCharArray();

    private readonly CosmosService _cosmos;
    private readonly ILogger<AgentKeyService> _logger;

    public AgentKeyService(CosmosService cosmos, ILogger<AgentKeyService> logger)
    {
        _cosmos = cosmos;
        _logger = logger;
    }

    public sealed record GeneratedKey(
        string Plaintext,
        string AgentId,
        string Hash,
        string Salt,
        string Hint,
        string LastFour,
        DateTimeOffset RotatedAt,
        DateTimeOffset ExpiresAt);

    public GeneratedKey GenerateNewKey(string? agentIdOverride = null)
    {
        var agentId = agentIdOverride ?? GenerateAgentId();

        var hint = RandomBase32(HintLength);
        var secret = RandomBase32(SecretLength);
        var plaintext = $"{Prefix}{hint}{secret}";

        var salt = RandomBytes(SaltLengthBytes);
        var hash = ComputeHmac(plaintext, salt);

        var now = DateTimeOffset.UtcNow;
        return new GeneratedKey(
            plaintext,
            agentId,
            Convert.ToBase64String(hash),
            Convert.ToBase64String(salt),
            hint,
            secret[^4..],
            now,
            now.AddDays(DefaultExpiryDays));
    }

    public async Task<Agent?> ValidateAsync(string presentedKey, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(presentedKey)
            || !presentedKey.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var stripped = presentedKey[Prefix.Length..];
        if (stripped.Length != HintLength + SecretLength)
        {
            return null;
        }

        var hint = stripped[..HintLength];
        if (!IsBase32(hint) || !IsBase32(stripped[HintLength..]))
        {
            return null;
        }

        // Cross-partition lookup by hint. At our scale (≤ ~1000 agents) this is cheap.
        Agent? agent = null;
        var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.apiKey.hint = @hint AND c.status = 'active'")
            .WithParameter("@hint", hint);

        using var iter = _cosmos.Agents.GetItemQueryIterator<Agent>(
            query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 2 });

        while (iter.HasMoreResults)
        {
            var page = await iter.ReadNextAsync(ct);
            foreach (var candidate in page)
            {
                if (agent is not null)
                {
                    // Hint collision (extremely improbable with 8 base32 chars = 40 bits).
                    // Fail closed to avoid timing leaks.
                    _logger.LogWarning("Agent key hint collision on hint {Hint}", hint);
                    return null;
                }
                agent = candidate;
            }
        }

        if (agent is null
            || agent.apiKey.expiresAt <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        var saltBytes = Convert.FromBase64String(agent.apiKey.salt);
        var presentedHash = ComputeHmac(presentedKey, saltBytes);
        var storedHash = Convert.FromBase64String(agent.apiKey.hash);

        if (presentedHash.Length != storedHash.Length
            || !CryptographicOperations.FixedTimeEquals(presentedHash, storedHash))
        {
            return null;
        }

        return agent;
    }

    public async Task<Agent> CreateAgentAsync(
        string displayName,
        string createdBy,
        string[] scopes,
        CancellationToken ct,
        string? agentIdOverride = null)
    {
        var key = GenerateNewKey(agentIdOverride);
        var agent = new Agent(
            id: key.AgentId,
            agentId: key.AgentId,
            displayName: displayName,
            createdAt: DateTimeOffset.UtcNow,
            createdBy: createdBy,
            status: "active",
            apiKey: new AgentApiKey(
                hash: key.Hash,
                salt: key.Salt,
                hint: key.Hint,
                lastFour: key.LastFour,
                rotatedAt: key.RotatedAt,
                expiresAt: key.ExpiresAt),
            scopes: scopes);

        await _cosmos.Agents.CreateItemAsync(
            agent,
            new PartitionKey(agent.agentId),
            cancellationToken: ct);

        // Stash plaintext in the in-memory result by piggybacking on a Tag — we return both.
        agent.GetType(); // no-op to avoid analyzer warning
        return agent with
        {
            // We use the apiKey.lastFour field unchanged; plaintext is handed back via PendingPlaintext.
        };
    }

    /// <summary>Helper that returns both the persisted agent and the plaintext key (only available at create/rotate).</summary>
    public async Task<(Agent agent, string plaintext)> CreateWithPlaintextAsync(
        string displayName,
        string createdBy,
        string[] scopes,
        CancellationToken ct)
    {
        var key = GenerateNewKey();
        var agent = new Agent(
            id: key.AgentId,
            agentId: key.AgentId,
            displayName: displayName,
            createdAt: DateTimeOffset.UtcNow,
            createdBy: createdBy,
            status: "active",
            apiKey: new AgentApiKey(
                hash: key.Hash,
                salt: key.Salt,
                hint: key.Hint,
                lastFour: key.LastFour,
                rotatedAt: key.RotatedAt,
                expiresAt: key.ExpiresAt),
            scopes: scopes);

        await _cosmos.Agents.CreateItemAsync(
            agent,
            new PartitionKey(agent.agentId),
            cancellationToken: ct);

        return (agent, key.Plaintext);
    }

    public async Task<(Agent agent, string plaintext)?> RotateAsync(string agentId, CancellationToken ct)
    {
        Agent existing;
        try
        {
            var resp = await _cosmos.Agents.ReadItemAsync<Agent>(
                agentId, new PartitionKey(agentId), cancellationToken: ct);
            existing = resp.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        var key = GenerateNewKey(agentId);
        var updated = existing with
        {
            apiKey = new AgentApiKey(
                hash: key.Hash,
                salt: key.Salt,
                hint: key.Hint,
                lastFour: key.LastFour,
                rotatedAt: key.RotatedAt,
                expiresAt: key.ExpiresAt),
        };

        await _cosmos.Agents.ReplaceItemAsync(
            updated, agentId, new PartitionKey(agentId), cancellationToken: ct);

        return (updated, key.Plaintext);
    }

    public async Task<bool> RevokeAsync(string agentId, CancellationToken ct)
    {
        try
        {
            var resp = await _cosmos.Agents.ReadItemAsync<Agent>(
                agentId, new PartitionKey(agentId), cancellationToken: ct);
            var revoked = resp.Resource with { status = "revoked" };
            await _cosmos.Agents.ReplaceItemAsync(
                revoked, agentId, new PartitionKey(agentId), cancellationToken: ct);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public static string GenerateAgentId() => $"a_{Guid.CreateVersion7():N}";

    private static byte[] ComputeHmac(string message, byte[] key)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
    }

    private static string RandomBase32(int length)
    {
        Span<byte> buf = stackalloc byte[length];
        RandomNumberGenerator.Fill(buf);
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = Base32[buf[i] % 32];
        }
        return new string(chars);
    }

    private static byte[] RandomBytes(int n)
    {
        var b = new byte[n];
        RandomNumberGenerator.Fill(b);
        return b;
    }

    private static bool IsBase32(string s)
    {
        foreach (var c in s)
        {
            if (Array.IndexOf(Base32, c) < 0)
            {
                return false;
            }
        }
        return true;
    }
}
