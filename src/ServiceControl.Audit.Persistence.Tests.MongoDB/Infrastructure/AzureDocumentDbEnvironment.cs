namespace ServiceControl.Audit.Persistence.Tests.MongoDB.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using global::MongoDB.Driver;

    /// <summary>
    /// Test environment for Azure DocumentDB using an external connection string.
    /// Set the AZURE_DOCUMENTDB_CONNECTION_STRING environment variable to run these tests.
    /// </summary>
    public class AzureDocumentDbEnvironment : IMongoTestEnvironment
    {
        const string ConnectionStringEnvVar = "AZURE_DOCUMENTDB_CONNECTION_STRING";

        string connectionString;

        public string ProductName => "Azure DocumentDB";

        public Task Initialize()
        {
            connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Environment variable '{ConnectionStringEnvVar}' is not set. " +
                    "Set this variable to an Azure DocumentDB connection string to run these tests.");
            }

            return Task.CompletedTask;
        }

        public string GetConnectionString() => connectionString;

        public string BuildConnectionString(string databaseName)
        {
            var builder = new MongoUrlBuilder(connectionString)
            {
                DatabaseName = databaseName
            };
            return builder.ToString();
        }

        public ExpectedCapabilities GetExpectedCapabilities() => new()
        {
            ProductName = "Azure DocumentDB",
            SupportsMultiCollectionBulkWrite = false, // MongoDB 8.0+ feature, not supported by Azure DocumentDB
            SupportsGridFS = false, // Azure DocumentDB does not support GridFS
            SupportsTextIndexes = true, // Uses PostgreSQL TSVector
            SupportsTransactions = true, // 30 second limit
            SupportsTtlIndexes = true,
            SupportsChangeStreams = true,
            MaxDocumentSizeBytes = 16 * 1024 * 1024
        };

        public Task Cleanup()
        {
            // No cleanup needed for external service
            return Task.CompletedTask;
        }

        /// <summary>
        /// Checks if the Azure DocumentDB environment is available.
        /// </summary>
        public static bool IsAvailable() =>
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringEnvVar));
    }
}
