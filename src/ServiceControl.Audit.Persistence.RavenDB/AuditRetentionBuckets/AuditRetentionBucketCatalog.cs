namespace ServiceControl.Audit.Persistence.RavenDB.AuditRetentionBuckets
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Durable manifest that allows restart recovery to discover the bucket collections and their
    /// dedicated static indexes. Stored as a single Raven document (no document expiry) so an
    /// interrupted cleanup can be resumed and orphaned buckets can be found after a restart.
    /// </summary>
    class AuditRetentionBucketCatalog
    {
        public const string DocumentId = "AuditRetentionBuckets/1";

        public string Id { get; set; }

        /// <summary>ISO 8601 duration the catalog was created with. Validated against the configured duration on load.</summary>
        public string BucketDuration { get; set; }

        public List<AuditRetentionBucket> Buckets { get; set; } = [];
    }

    class AuditRetentionBucket
    {
        public string Key { get; set; }

        public DateTime Start { get; set; }

        public DateTime End { get; set; }

        public AuditRetentionBucketState State { get; set; }

        public string ProcessedMessageCollection { get; set; }

        public string SagaSnapshotCollection { get; set; }

        public string MessagesViewIndex { get; set; }

        public string MessagesViewFullTextIndex { get; set; }

        public string SagaDetailsIndex { get; set; }
    }

    enum AuditRetentionBucketState
    {
        Active,
        Retired
    }

    static class AuditRetentionBucketNaming
    {
        public static DateTime GetBucketStart(DateTime utcNow, TimeSpan bucketDuration)
        {
            var bucketTicks = utcNow.Ticks / bucketDuration.Ticks * bucketDuration.Ticks;
            return new DateTime(bucketTicks, DateTimeKind.Utc);
        }

        // The bucket key is derived from the bucket start aligned to the configured bucket duration.
        // The duration is validated to be a whole number of hours of at least one hour, so the
        // hour-granular key stays collision-free. The format is part of the persisted catalog
        // contract: changing it would orphan existing buckets.
        public static string GetBucketKey(DateTime bucketStart) => bucketStart.ToString("yyyyMMdd_HH", CultureInfo.InvariantCulture);

        public static string GetProcessedMessageCollection(string bucketKey) => $"ProcessedMessages_{bucketKey}";

        public static string GetSagaSnapshotCollection(string bucketKey) => $"SagaSnapshots_{bucketKey}";

        public static string GetMessagesViewIndexName(string bucketKey) => $"MessagesViewIndex_{bucketKey}";

        public static string GetMessagesViewFullTextIndexName(string bucketKey) => $"MessagesViewIndexWithFullTextSearch_{bucketKey}";

        public static string GetSagaDetailsIndexName(string bucketKey) => $"SagaDetailsIndex_{bucketKey}";
    }
}
