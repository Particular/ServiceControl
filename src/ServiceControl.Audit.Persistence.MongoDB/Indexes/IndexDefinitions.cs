namespace ServiceControl.Audit.Persistence.MongoDB.Indexes
{
    using System;
    using Documents;
    using global::MongoDB.Driver;

    static class IndexDefinitions
    {
        public static CreateIndexModel<ProcessedMessageDocument>[] ProcessedMessages =>
        [
            // Primary sort index for default message listing
            new(
                Builders<ProcessedMessageDocument>.IndexKeys.Descending(x => x.ProcessedAt),
                new CreateIndexOptions { Name = "processedAt_desc" }),

            // Alternative sort by time sent
            new(
                Builders<ProcessedMessageDocument>.IndexKeys.Descending("messageMetadata.TimeSent"),
                new CreateIndexOptions { Name = "timeSent_desc" }),

            // Compound index for filtering by endpoint with processedAt sort
            new(
                Builders<ProcessedMessageDocument>.IndexKeys
                    .Ascending("messageMetadata.ReceivingEndpoint.Name")
                    .Descending(x => x.ProcessedAt),
                new CreateIndexOptions { Name = "endpoint_processedAt" }),

            // Conversation queries (sparse since not all messages have conversations)
            new(
                Builders<ProcessedMessageDocument>.IndexKeys.Ascending("messageMetadata.ConversationId"),
                new CreateIndexOptions { Name = "conversationId", Sparse = true }),

            // TTL index for automatic document expiration
            new(
                Builders<ProcessedMessageDocument>.IndexKeys.Ascending(x => x.ExpiresAt),
                new CreateIndexOptions { Name = "ttl_expiry", ExpireAfter = TimeSpan.Zero })
        ];

        public static CreateIndexModel<SagaSnapshotDocument>[] SagaSnapshots =>
        [
            // SagaHistory aggregation queries
            new(
                Builders<SagaSnapshotDocument>.IndexKeys.Ascending(x => x.SagaId),
                new CreateIndexOptions { Name = "sagaId" }),

            // TTL index for automatic document expiration
            new(
                Builders<SagaSnapshotDocument>.IndexKeys.Ascending(x => x.ExpiresAt),
                new CreateIndexOptions { Name = "ttl_expiry", ExpireAfter = TimeSpan.Zero })
        ];

        public static CreateIndexModel<KnownEndpointDocument>[] KnownEndpoints =>
        [
            // Compound index for endpoint lookup (also serves as unique constraint via _id)
            new(
                Builders<KnownEndpointDocument>.IndexKeys
                    .Ascending(x => x.Name)
                    .Ascending(x => x.HostId),
                new CreateIndexOptions { Name = "name_hostId" }),

            // TTL index for automatic document expiration
            new(
                Builders<KnownEndpointDocument>.IndexKeys.Ascending(x => x.ExpiresAt),
                new CreateIndexOptions { Name = "ttl_expiry", ExpireAfter = TimeSpan.Zero })
        ];

        public static CreateIndexModel<MessageBodyDocument>[] MessageBodies =>
        [
            // Note: MessageBodies don't have an ExpiresAt field currently
            // Bodies are cleaned up when their parent ProcessedMessage expires
            // If we want TTL on bodies, we'd need to add ExpiresAt to MessageBodyDocument
        ];

        public static CreateIndexModel<FailedAuditImportDocument>[] FailedAuditImports =>
        [
            // No additional indexes needed - queries are by _id only
        ];
    }
}
