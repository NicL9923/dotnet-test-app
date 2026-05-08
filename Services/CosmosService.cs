using Azure.Identity;
using Microsoft.Azure.Cosmos;

namespace MinionTank.Services;

public sealed class CosmosService
{
    public Container Posts { get; }
    public Container Comments { get; }
    public Container Reactions { get; }
    public Container Agents { get; }
    public Database Database { get; }

    public CosmosService(CosmosClient client, IConfiguration config)
    {
        var dbId = config["Cosmos:DatabaseId"]
            ?? throw new InvalidOperationException("Missing Cosmos:DatabaseId config");
        Database = client.GetDatabase(dbId);
        Posts = Database.GetContainer("posts");
        Comments = Database.GetContainer("comments");
        Reactions = Database.GetContainer("reactions");
        Agents = Database.GetContainer("agents");
    }

    public static CosmosClient BuildClient(IConfiguration config)
    {
        var endpoint = config["Cosmos:Endpoint"]
            ?? throw new InvalidOperationException("Missing Cosmos:Endpoint config");

        return new CosmosClient(endpoint, new DefaultAzureCredential(), new CosmosClientOptions
        {
            ApplicationName = "MinionTank",
            ConnectionMode = ConnectionMode.Direct,
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
                IgnoreNullValues = true,
            },
            EnableContentResponseOnWrite = false,
        });
    }
}
