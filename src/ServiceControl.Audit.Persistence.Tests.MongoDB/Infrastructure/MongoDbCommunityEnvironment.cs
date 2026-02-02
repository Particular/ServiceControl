namespace ServiceControl.Audit.Persistence.Tests.MongoDB.Infrastructure
{
    using System.Threading.Tasks;
    using global::MongoDB.Driver;
    using ServiceControl.Audit.Persistence.Tests;

    /// <summary>
    /// Test environment for MongoDB Community/Enterprise using Docker via Testcontainers.
    /// </summary>
    public class MongoDbCommunityEnvironment : IMongoTestEnvironment
    {
        public string ProductName => "MongoDB";

        public async Task Initialize()
        {
            _ = await SharedMongoDbContainer.GetInstance().ConfigureAwait(false);
        }

        public string GetConnectionString() => SharedMongoDbContainer.GetConnectionString();

        public string BuildConnectionString(string databaseName)
        {
            var builder = new MongoUrlBuilder(GetConnectionString())
            {
                DatabaseName = databaseName
            };
            return builder.ToString();
        }

        public ExpectedCapabilities GetExpectedCapabilities() => new()
        {
            ProductName = "MongoDB Community",
            SupportsMultiCollectionBulkWrite = true, // MongoDB 8.0+ feature, container uses 8.0
            SupportsGridFS = true,
            SupportsTextIndexes = true,
            SupportsTransactions = true,
            SupportsTtlIndexes = true,
            SupportsChangeStreams = true,
            MaxDocumentSizeBytes = 16 * 1024 * 1024
        };

        public Task Cleanup()
        {
            // Container is shared across tests and cleaned up at assembly level
            return Task.CompletedTask;
        }
    }
}
