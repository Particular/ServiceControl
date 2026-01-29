namespace ServiceControl.Audit.Persistence.Tests.MongoDB.Infrastructure
{
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence.MongoDB.ProductCapabilities;

    /// <summary>
    /// Abstracts the test environment for different MongoDB-compatible products.
    /// Provides connection strings and expected capabilities for each product.
    /// </summary>
    public interface IMongoTestEnvironment
    {
        /// <summary>
        /// Gets the name of the product being tested.
        /// </summary>
        string ProductName { get; }

        /// <summary>
        /// Initializes the test environment (e.g., starts container, validates connection string).
        /// </summary>
        Task Initialize();

        /// <summary>
        /// Gets the connection string for the test environment.
        /// </summary>
        string GetConnectionString();

        /// <summary>
        /// Builds a connection string with the specified database name.
        /// </summary>
        string BuildConnectionString(string databaseName);

        /// <summary>
        /// Gets the expected product capabilities for assertion.
        /// </summary>
        ExpectedCapabilities GetExpectedCapabilities();

        /// <summary>
        /// Cleans up the test environment.
        /// </summary>
        Task Cleanup();
    }

    /// <summary>
    /// Expected capabilities for a MongoDB-compatible product.
    /// Used for test assertions.
    /// </summary>
    public class ExpectedCapabilities
    {
        public required string ProductName { get; init; }
        public required bool SupportsMultiCollectionBulkWrite { get; init; }
        public required bool SupportsGridFS { get; init; }
        public required bool SupportsTextIndexes { get; init; }
        public required bool SupportsTransactions { get; init; }
        public required bool SupportsTtlIndexes { get; init; }
        public required bool SupportsChangeStreams { get; init; }
        public required int MaxDocumentSizeBytes { get; init; }
    }
}
