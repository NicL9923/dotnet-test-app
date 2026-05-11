using System.Net;
using Microsoft.Azure.Cosmos;
using MinionTank.Models;

namespace MinionTank.Services;

public static class AgentDirectory
{
    public static async Task<IReadOnlyDictionary<string, AuthorSummary>> LoadAuthorSummariesAsync(
        CosmosService cosmos,
        IEnumerable<string> agentIds,
        CancellationToken ct)
    {
        var summaries = new Dictionary<string, AuthorSummary>(StringComparer.Ordinal);
        foreach (var agentId in agentIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal))
        {
            try
            {
                var resp = await cosmos.Agents.ReadItemAsync<Agent>(
                    agentId,
                    new PartitionKey(agentId),
                    cancellationToken: ct);
                summaries[agentId] = ToAuthorSummary(resp.Resource);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                summaries[agentId] = UnknownAuthor(agentId);
            }
        }

        return summaries;
    }

    public static AuthorSummary UnknownAuthor(string agentId) =>
        new(agentId, agentId, "", agentId);

    public static AuthorSummary GetAuthor(
        IReadOnlyDictionary<string, AuthorSummary> summaries,
        string agentId) =>
        summaries.TryGetValue(agentId, out var author)
            ? author
            : UnknownAuthor(agentId);

    public static async Task<List<Agent>> QueryOwnerAgentsAsync(
        CosmosService cosmos,
        string owner,
        CancellationToken ct)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.createdBy = @owner")
            .WithParameter("@owner", owner);

        var agents = new List<Agent>();
        using var iter = cosmos.Agents.GetItemQueryIterator<Agent>(query);
        while (iter.HasMoreResults)
        {
            var page = await iter.ReadNextAsync(ct);
            agents.AddRange(page);
        }

        return agents;
    }

    public static async Task<Agent?> ReadOwnedAgentAsync(
        CosmosService cosmos,
        string agentId,
        string owner,
        CancellationToken ct)
    {
        try
        {
            var resp = await cosmos.Agents.ReadItemAsync<Agent>(
                agentId,
                new PartitionKey(agentId),
                cancellationToken: ct);
            return string.Equals(resp.Resource.createdBy, owner, StringComparison.OrdinalIgnoreCase)
                ? resp.Resource
                : null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public static AuthorSummary ToAuthorSummary(Agent agent)
    {
        var firstName = FirstNameFromOwner(agent.createdByName, agent.createdBy);
        var label = string.IsNullOrWhiteSpace(firstName)
            ? agent.displayName
            : $"{firstName}'s {agent.displayName}";
        return new AuthorSummary(agent.agentId, agent.displayName, firstName, label);
    }

    public static string FirstNameFromOwner(string? createdByName, string createdBy)
    {
        // Prefer the friendly display name when we have it — it has real word breaks.
        var preferred = !string.IsNullOrWhiteSpace(createdByName) ? createdByName : null;
        if (preferred is not null)
        {
            var trimmed = preferred.Trim();
            var first = trimmed.Split([' ', '.', '_', '-'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            return Capitalize(first);
        }

        // Fallback to the UPN local part — but only return a name if the local part has a
        // real separator. Smashed-together UPNs like `firstlast@…` aren't reliable.
        if (string.IsNullOrWhiteSpace(createdBy))
        {
            return "";
        }
        var idTrimmed = createdBy.Trim();
        var namePart = idTrimmed.Contains('@', StringComparison.Ordinal)
            ? idTrimmed[..idTrimmed.IndexOf('@')]
            : idTrimmed;
        if (!namePart.Contains('.') && !namePart.Contains('_') && !namePart.Contains('-') && !namePart.Contains(' '))
        {
            return "";
        }
        var fallback = namePart.Split([' ', '.', '_', '-'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return Capitalize(fallback);
    }

    private static string Capitalize(string? part)
    {
        if (string.IsNullOrWhiteSpace(part))
        {
            return "";
        }
        return part.Length == 1
            ? part.ToUpperInvariant()
            : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant();
    }
}
