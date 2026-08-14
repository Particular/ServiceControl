namespace ServiceControl.Audit.Persistence.RavenDB.AuditRetentionBuckets
{
    using System.Collections.Generic;
    using Raven.Client.Documents.Indexes;

    /// <summary>
    /// Builds the dedicated static index definitions for a single retention bucket. Each bucket gets
    /// uniquely named indexes that map exclusively over the bucket's collections, so expiring a bucket
    /// only requires deleting its own indexes instead of rebuilding a shared full-text index.
    /// </summary>
    static class AuditRetentionBucketIndexes
    {
        public static IndexDefinition MessagesView(AuditRetentionBucket bucket) => new()
        {
            Name = bucket.MessagesViewIndex,
            Maps =
            {
                $$"""
                from message in docs["{{bucket.ProcessedMessageCollection}}"]
                select new
                {
                    MessageId = (string)message.MessageMetadata["MessageId"],
                    MessageType = (string)message.MessageMetadata["MessageType"],
                    IsSystemMessage = (bool)message.MessageMetadata["IsSystemMessage"],
                    Status = (bool)message.MessageMetadata["IsRetried"] ? "ResolvedSuccessfully" : "Successful",
                    TimeSent = (System.DateTime)message.MessageMetadata["TimeSent"],
                    ProcessedAt = message.ProcessedAt,
                    ReceivingEndpointName = ((message.MessageMetadata["ReceivingEndpoint"])).Name,
                    CriticalTime = (System.TimeSpan?)message.MessageMetadata["CriticalTime"],
                    ProcessingTime = (System.TimeSpan?)message.MessageMetadata["ProcessingTime"],
                    DeliveryTime = (System.TimeSpan?)message.MessageMetadata["DeliveryTime"],
                    Query = message.MessageMetadata.Select(_ => _.Value.ToString()).Union(new[]
                    {
                        string.Join(" ", message.Headers.Select(x => x.Value))
                    }).ToArray(),
                    ConversationId = (string)message.MessageMetadata["ConversationId"]
                }
                """
            },
            Fields = new Dictionary<string, IndexFieldOptions>
            {
                ["Query"] = new IndexFieldOptions
                {
                    Indexing = FieldIndexing.Search,
                    Analyzer = MessagesViewQueryAnalyzer
                }
            }
        };

        public static IndexDefinition MessagesViewWithFullTextSearch(AuditRetentionBucket bucket) => new()
        {
            Name = bucket.MessagesViewFullTextIndex,
            Maps =
            {
                $$"""
                from message in docs["{{bucket.ProcessedMessageCollection}}"]
                select new
                {
                    MessageId = (string)message.MessageMetadata["MessageId"],
                    MessageType = (string)message.MessageMetadata["MessageType"],
                    IsSystemMessage = (bool)message.MessageMetadata["IsSystemMessage"],
                    Status = (bool)message.MessageMetadata["IsRetried"] ? "ResolvedSuccessfully" : "Successful",
                    TimeSent = (System.DateTime)message.MessageMetadata["TimeSent"],
                    ProcessedAt = message.ProcessedAt,
                    ReceivingEndpointName = ((message.MessageMetadata["ReceivingEndpoint"])).Name,
                    CriticalTime = (System.TimeSpan?)message.MessageMetadata["CriticalTime"],
                    ProcessingTime = (System.TimeSpan?)message.MessageMetadata["ProcessingTime"],
                    DeliveryTime = (System.TimeSpan?)message.MessageMetadata["DeliveryTime"],
                    Query = message.MessageMetadata.Select(_ => _.Value.ToString()).Union(new[]
                    {
                        string.Join(" ", message.Headers.Select(x => x.Value)),
                        LoadAttachment(message, "body").GetContentAsString()
                    }).ToArray(),
                    ConversationId = (string)message.MessageMetadata["ConversationId"]
                }
                """
            },
            Fields = new Dictionary<string, IndexFieldOptions>
            {
                ["Query"] = new IndexFieldOptions
                {
                    Indexing = FieldIndexing.Search,
                    Analyzer = MessagesViewQueryAnalyzer
                }
            }
        };

        public static IndexDefinition SagaDetails(AuditRetentionBucket bucket) => new()
        {
            Name = bucket.SagaDetailsIndex,
            Maps =
            {
                $$"""
                from doc in docs["{{bucket.SagaSnapshotCollection}}"]
                select new
                {
                    doc.SagaId,
                    Id = doc.SagaId,
                    doc.SagaType,
                    Changes = new[]
                    {
                        new
                        {
                            Endpoint = doc.Endpoint,
                            FinishTime = doc.FinishTime,
                            InitiatingMessage = doc.InitiatingMessage,
                            OutgoingMessages = doc.OutgoingMessages,
                            StartTime = doc.StartTime,
                            StateAfterChange = doc.StateAfterChange,
                            Status = doc.Status
                        }
                    }
                }
                """
            },
            Reduce = """
                from result in results
                group result by result.SagaId
                into g
                let first = g.First()
                select new
                {
                    Id = first.SagaId,
                    SagaId = first.SagaId,
                    SagaType = first.SagaType,
                    Changes = g.SelectMany(x => x.Changes)
                        .OrderByDescending(x => x.FinishTime)
                        .Take(50000)
                        .ToList()
                }
                """,
            Fields = new Dictionary<string, IndexFieldOptions>
            {
                ["SagaId"] = new IndexFieldOptions { Indexing = FieldIndexing.Exact },
                ["SagaType"] = new IndexFieldOptions { Indexing = FieldIndexing.No },
                ["Changes"] = new IndexFieldOptions { Indexing = FieldIndexing.No }
            }
        };

        // Same analyzer as the shared MessagesView indexes. Changing it would force a full rebuild of
        // every bucket index, so it must stay in sync with MessagesViewIndex.
        const string MessagesViewQueryAnalyzer = "Lucene.Net.Analysis.Standard.StandardAnalyzer, Lucene.Net, Version=3.0.3.0, Culture=neutral, PublicKeyToken=85089178b9ac3181";
    }
}
