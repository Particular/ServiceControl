#nullable enable

namespace ServiceControl.Audit.Persistence.MongoDB.ProductCapabilities
{
    using System;

    /// <summary>
    /// Capabilities for Amazon DocumentDB.
    /// Note: Elastic clusters have additional limitations (no GridFS, no change streams).
    /// This class represents standard (non-Elastic) cluster capabilities.
    /// </summary>
    public class AmazonDocumentDbCapabilities : IMongoProductCapabilities
    {
        public AmazonDocumentDbCapabilities(bool isElasticCluster = false, Version? serverVersion = null)
        {
            IsElasticCluster = isElasticCluster;
            ServerVersion = serverVersion;
        }

        public bool IsElasticCluster { get; }

        public string ProductName => IsElasticCluster ? "Amazon DocumentDB (Elastic)" : "Amazon DocumentDB";
        public Version? ServerVersion { get; }

        // TODO: 2026-02-02 - Doco states that this is not supported in Amazon DocumentDB
        public bool SupportsMultiCollectionBulkWrite => false;

        // GridFS not supported on Elastic clusters
        public bool SupportsGridFS => !IsElasticCluster;

        // Text search is basic: English only, no wildcards, no term exclusion
        public bool SupportsTextIndexes => true;

        // Transactions supported with 1 minute limit and 32MB transaction log size
        public bool SupportsTransactions => true;

        public bool SupportsTtlIndexes => true;

        // Change streams not supported on Elastic clusters
        public bool SupportsChangeStreams => !IsElasticCluster;

        public int MaxDocumentSizeBytes => 16 * 1024 * 1024; // 16MB

        // $facet aggregation is NOT supported on Amazon DocumentDB
        public bool SupportsFacetAggregation => false;
    }
}
