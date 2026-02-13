#nullable enable

namespace ServiceControl.Audit.Persistence.MongoDB.ProductCapabilities
{
    using System;

    /// <summary>
    /// Capabilities for Azure DocumentDB (new Jan 2025 product).
    /// Built on PostgreSQL with MongoDB wire protocol compatibility.
    /// </summary>
    public class AzureDocumentDbCapabilities(Version? serverVersion = null) : IMongoProductCapabilities
    {
        public string ProductName => "Azure DocumentDB";
        public Version? ServerVersion { get; } = serverVersion;
        public bool SupportsMultiCollectionBulkWrite => false;
        public bool SupportsTextIndexes => true;
        public bool SupportsTtlIndexes => true;
        public int MaxDocumentSizeBytes => 16 * 1024 * 1024; // 16MB
        public bool SupportsFacetAggregation => true;
        public bool SupportsWiredTigerCacheMetrics => false;
    }
}
