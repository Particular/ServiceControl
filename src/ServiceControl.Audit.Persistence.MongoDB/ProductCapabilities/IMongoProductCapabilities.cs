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
        /// </summary>
        bool SupportsMultiCollectionBulkWrite { get; }

        /// <summary>
        /// Whether text indexes are supported for full-text search.
        /// </summary>
        bool SupportsTextIndexes { get; }

        /// <summary>
        /// Whether TTL (Time-To-Live) indexes are supported for automatic document expiration.
        /// </summary>
        bool SupportsTtlIndexes { get; }

        /// <summary>
        /// Maximum document size in bytes.
        /// Standard is 16MB (16,777,216 bytes) for all supported products.
        /// </summary>
        int MaxDocumentSizeBytes { get; }

        /// <summary>
        /// Whether the $facet aggregation stage is supported.
        /// When $facet is not supported, use multiple queries instead.
        /// </summary>
        bool SupportsFacetAggregation { get; }

        /// <summary>
        /// Whether WiredTiger cache metrics are available via serverStatus.
        /// When true, dirty cache percentage is used for ingestion backpressure detection.
        /// When false, write latency tracking is used instead.
        /// </summary>
        bool SupportsWiredTigerCacheMetrics { get; }
    }
}
