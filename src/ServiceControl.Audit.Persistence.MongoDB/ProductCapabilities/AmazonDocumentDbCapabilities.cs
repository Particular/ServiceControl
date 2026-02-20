#nullable enable

namespace ServiceControl.Audit.Persistence.MongoDB.ProductCapabilities
{
    using System;

    /// <summary>
    /// Capabilities for Amazon DocumentDB.
    /// This class represents standard (non-Elastic) cluster capabilities.
    /// </summary>
    public class AmazonDocumentDbCapabilities(bool isElasticCluster = false, Version? serverVersion = null) : IMongoProductCapabilities
    {
        public bool IsElasticCluster { get; } = isElasticCluster;
        public string ProductName => IsElasticCluster ? "Amazon DocumentDB (Elastic)" : "Amazon DocumentDB";
        public Version? ServerVersion { get; } = serverVersion;
        public bool SupportsMultiCollectionBulkWrite => false;
        public bool SupportsTextIndexes => true;
        public bool SupportsTtlIndexes => true;
        public int MaxDocumentSizeBytes => 16 * 1024 * 1024; // 16MB
        public bool SupportsFacetAggregation => false;
        public bool SupportsWiredTigerCacheMetrics => false;
    }
}
