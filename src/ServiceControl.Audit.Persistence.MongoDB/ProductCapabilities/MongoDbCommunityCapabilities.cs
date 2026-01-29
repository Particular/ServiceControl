#nullable enable

namespace ServiceControl.Audit.Persistence.MongoDB.ProductCapabilities
{
    using System;

    /// <summary>
    /// Capabilities for MongoDB Community/Enterprise.
    /// Full feature support.
    /// </summary>
    public class MongoDbCommunityCapabilities : IMongoProductCapabilities
    {
        public MongoDbCommunityCapabilities(Version? serverVersion = null)
        {
            ServerVersion = serverVersion;
        }

        public string ProductName => "MongoDB Community";
        public Version? ServerVersion { get; }
        public bool SupportsMultiCollectionBulkWrite => ServerVersion >= MongoVersions.Version8;
        public bool SupportsGridFS => true;
        public bool SupportsTextIndexes => true;
        public bool SupportsTransactions => true;
        public bool SupportsTtlIndexes => true;
        public bool SupportsChangeStreams => true;
        public int MaxDocumentSizeBytes => 16 * 1024 * 1024; // 16MB
    }
}
