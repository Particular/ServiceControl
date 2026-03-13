namespace ServiceControl.Audit.Persistence.MongoDB
{
    using global::MongoDB.Driver;
    using ProductCapabilities;

    /// <summary>
    /// Provides access to the MongoDB client and database.
    /// </summary>
    public interface IMongoClientProvider
    {
        /// <summary>
        /// Gets the MongoDB client instance.
        /// </summary>
        IMongoClient Client { get; }

        /// <summary>
        /// Gets the configured database.
        /// </summary>
        IMongoDatabase Database { get; }

        /// <summary>
        /// Gets the detected product capabilities for the connected MongoDB-compatible database.
        /// </summary>
        IMongoProductCapabilities ProductCapabilities { get; }
    }
}
