#nullable enable

namespace ServiceControl.Audit.Persistence.MongoDB.ProductCapabilities
{
    using System;

    /// <summary>
    /// Known MongoDB versions for feature detection.
    /// </summary>
    public static class MongoVersions
    {
        /// <summary>
        /// MongoDB 8.0 - Introduces multi-collection bulk write operations.
        /// </summary>
        public static readonly Version Version8 = new(8, 0);
    }

    /// <summary>
    /// Abstraction for product-specific capabilities across MongoDB-compatible databases.
    /// Different products (MongoDB, Azure DocumentDB, Amazon DocumentDB) have varying
    /// feature support levels.
    /// </summary>
    public interface IMongoProductCapabilities
    {
        /// <summary>
        /// The name of the MongoDB-compatible product.
        /// </summary>
        string ProductName { get; }

        /// <summary>
        /// The server version, if known. May be null for cloud products where
        /// version information is not available or meaningful.
        /// </summary>
        Version? ServerVersion { get; }

        /// <summary>
        /// Whether multi-collection bulk write operations are supported.
        /// This is a MongoDB 8.0+ feature that allows writing to multiple collections
        /// in a single operation.
        /// </summary>
        bool SupportsMultiCollectionBulkWrite { get; }

        /// <summary>
        /// Whether GridFS is supported for storing large binary data.
        /// MongoDB and Amazon DocumentDB (non-Elastic) support GridFS.
        /// Azure DocumentDB does not support GridFS.
        /// </summary>
        bool SupportsGridFS { get; }

        /// <summary>
        /// Whether text indexes are supported for full-text search.
        /// All supported products have some level of text index support,
        /// but capabilities vary (e.g., language support, operators).
        /// </summary>
        bool SupportsTextIndexes { get; }

        /// <summary>
        /// Whether multi-document transactions are supported.
        /// All supported products support transactions with varying limits.
        /// </summary>
        bool SupportsTransactions { get; }

        /// <summary>
        /// Whether TTL (Time-To-Live) indexes are supported for automatic document expiration.
        /// All supported products support TTL indexes.
        /// </summary>
        bool SupportsTtlIndexes { get; }

        /// <summary>
        /// Whether change streams are supported for real-time data notifications.
        /// MongoDB and Azure DocumentDB support change streams.
        /// Amazon DocumentDB Elastic clusters do not support change streams.
        /// </summary>
        bool SupportsChangeStreams { get; }

        /// <summary>
        /// Maximum document size in bytes.
        /// Standard is 16MB (16,777,216 bytes) for all supported products.
        /// </summary>
        int MaxDocumentSizeBytes { get; }
    }
}
