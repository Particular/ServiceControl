#nullable enable

namespace ServiceControl.Audit.Persistence.MongoDB.ProductCapabilities
{
    using System;

    /// <summary>
    /// Capabilities for MongoDB Community/Enterprise.
    /// Full feature support.
    /// </summary>
    public class MongoDbCommunityCapabilities(Version? serverVersion = null) : IMongoProductCapabilities
    {
        public string ProductName => "MongoDB Community";
        public Version? ServerVersion { get; } = serverVersion;
        public bool SupportsMultiCollectionBulkWrite => ServerVersion >= MongoVersions.Version8;
        public bool SupportsTextIndexes => true;
        public bool SupportsTtlIndexes => true;
        public int MaxDocumentSizeBytes => 16 * 1024 * 1024; // 16MB
        public bool SupportsFacetAggregation => true;
        public bool SupportsWiredTigerCacheMetrics => true;
    }
}
