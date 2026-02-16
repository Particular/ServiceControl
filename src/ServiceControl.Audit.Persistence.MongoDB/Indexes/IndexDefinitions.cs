namespace ServiceControl.Audit.Persistence.MongoDB.Indexes
{
    using System;
    using System.Collections.Generic;
    using Documents;
    using global::MongoDB.Driver;

    static class IndexDefinitions
    {
        public static CreateIndexModel<ProcessedMessageDocument>[] GetProcessedMessageIndexes()
        {
            // Text index covers metadata fields and header values. Body text search is handled
            // by the separate messageBodies collection to avoid write path overhead.
            // Note: headerSearchTokens is a flattened string array of header values because
            // NServiceBus header keys contain dots/$, which MongoDB's text index cannot traverse.
            var textIndexKeys = Builders<ProcessedMessageDocument>.IndexKeys
                .Text("messageMetadata.MessageId")
                .Text("messageMetadata.MessageType")
                .Text("messageMetadata.ConversationId")
                .Text("headerSearchTokens");

            return
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
                    new CreateIndexOptions { Name = "ttl_expiry", ExpireAfter = TimeSpan.Zero }),

                // Compound text index for full-text search
                // Indexes metadata fields directly (no duplication) plus header values (and optionally body content)
                new(textIndexKeys, new CreateIndexOptions { Name = "text_search" })
            ];
        }

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

        public static CreateIndexModel<MessageBodyDocument>[] GetMessageBodyIndexes(bool enableFullTextSearch)
        {
            var indexes = new List<CreateIndexModel<MessageBodyDocument>>
            {
                // TTL index for automatic document expiration
                new(
                    Builders<MessageBodyDocument>.IndexKeys.Ascending(x => x.ExpiresAt),
                    new CreateIndexOptions { Name = "ttl_expiry", ExpireAfter = TimeSpan.Zero })
            };

            if (enableFullTextSearch)
            {
                // Text index on body content for full-text search (async, eventual consistency)
                indexes.Add(new(
                    Builders<MessageBodyDocument>.IndexKeys.Text(x => x.TextBody),
                    new CreateIndexOptions { Name = "body_text_search" }));
            }

            return [.. indexes];
        }

        public static CreateIndexModel<FailedAuditImportDocument>[] FailedAuditImports =>
        [
            // No additional indexes needed - queries are by _id only
        ];
    }
}
