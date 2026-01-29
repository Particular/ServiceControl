namespace ServiceControl.Audit.Persistence.Tests.MongoDB.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using global::MongoDB.Driver;

    /// <summary>
    /// Test environment for Amazon DocumentDB using an external connection string.
    /// Set the AWS_DOCUMENTDB_CONNECTION_STRING environment variable to run these tests.
    /// Optionally set AWS_DOCUMENTDB_IS_ELASTIC=true for Elastic cluster testing.
    /// </summary>
    public class AmazonDocumentDbEnvironment : IMongoTestEnvironment
    {
        const string ConnectionStringEnvVar = "AWS_DOCUMENTDB_CONNECTION_STRING";
        const string IsElasticEnvVar = "AWS_DOCUMENTDB_IS_ELASTIC";

        string connectionString;
        bool isElasticCluster;

        public string ProductName => isElasticCluster ? "Amazon DocumentDB (Elastic)" : "Amazon DocumentDB";

        public Task Initialize()
        {
            connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Environment variable '{ConnectionStringEnvVar}' is not set. " +
                    "Set this variable to an Amazon DocumentDB connection string to run these tests.");
            }

            var isElasticValue = Environment.GetEnvironmentVariable(IsElasticEnvVar);
            isElasticCluster = string.Equals(isElasticValue, "true", StringComparison.OrdinalIgnoreCase);

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
            ProductName = isElasticCluster ? "Amazon DocumentDB (Elastic)" : "Amazon DocumentDB",
            SupportsMultiCollectionBulkWrite = false, // MongoDB 8.0+ feature, not supported by DocumentDB
            SupportsGridFS = !isElasticCluster, // Elastic clusters do NOT support GridFS
            SupportsTextIndexes = true, // English only
            SupportsTransactions = true, // 1 minute limit
            SupportsTtlIndexes = true,
            SupportsChangeStreams = !isElasticCluster, // Elastic clusters do NOT support change streams
            MaxDocumentSizeBytes = 16 * 1024 * 1024
        };

        public Task Cleanup()
        {
            // No cleanup needed for external service
            return Task.CompletedTask;
        }

        /// <summary>
        /// Checks if the Amazon DocumentDB environment is available.
        /// </summary>
        public static bool IsAvailable() =>
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringEnvVar));
    }
}
