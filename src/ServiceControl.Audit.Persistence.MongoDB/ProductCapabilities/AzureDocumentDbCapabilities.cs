#nullable enable

namespace ServiceControl.Audit.Persistence.MongoDB.ProductCapabilities
{
    using System;

    /// <summary>
    /// Capabilities for Azure DocumentDB (new Jan 2025 product).
    /// Built on PostgreSQL with MongoDB wire protocol compatibility.
    /// </summary>
    public class AzureDocumentDbCapabilities : IMongoProductCapabilities
    {
        public AzureDocumentDbCapabilities(Version? serverVersion = null)
        {
            ServerVersion = serverVersion;
        }

        public string ProductName => "Azure DocumentDB";
        public Version? ServerVersion { get; }

        // TODO: 2026-02-02 - Doco states that this is not supported in Azure DocumentDB
        public bool SupportsMultiCollectionBulkWrite => false;

        // GridFS is not documented/supported - use embedded document storage
        public bool SupportsGridFS => false;

        // Text search uses PostgreSQL TSVector under the hood
        // 15+ languages supported, but only ONE text index per collection
        public bool SupportsTextIndexes => true;

        // Transactions supported with 30 second limit
        public bool SupportsTransactions => true;

        public bool SupportsTtlIndexes => true;
        public bool SupportsChangeStreams => true;
        public int MaxDocumentSizeBytes => 16 * 1024 * 1024; // 16MB
        public bool SupportsFacetAggregation => true;
    }
}
