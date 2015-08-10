namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.IdGeneration;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;
    using Raven.Client.Linq;
    using Raven.Json.Linq;
    using ServiceControl.MessageFailures;

    public class RetryDocumentManager
    {
        public IDocumentStore Store { get; set; }

        static string RetrySessionId = CombGuid.Generate().ToString();

        public string CreateBatchDocument(string context = null)
        {
            var batchDocumentId = RetryBatch.MakeDocumentId(CombGuid.Generate().ToString());
            using (var session = Store.OpenSession())
            {
                session.Store(new RetryBatch
                {
                    Id = batchDocumentId, 
                    Context = context,
                    RetrySessionId = RetrySessionId, 
                    Status = RetryBatchStatus.MarkingDocuments
                });
                session.SaveChanges();
            }
            return batchDocumentId;
        }

        public string CreateFailedMessageRetryDocument(string batchDocumentId, string messageUniqueId)
        {
            var failureRetryId = FailedMessageRetry.MakeDocumentId(messageUniqueId);
            Store.DatabaseCommands.Patch(failureRetryId,
                new PatchRequest[0], // if existing do nothing
                new[]
                {
                    new PatchRequest
                    {
                        Name = "FailedMessageId",
                        Type = PatchCommandType.Set,
                        Value = FailedMessage.MakeDocumentId(messageUniqueId)
                    }, 
                    new PatchRequest
                    {
                        Name = "RetryBatchId", 
                        Type = PatchCommandType.Set, 
                        Value = batchDocumentId
                    }
                },
                RavenJObject.Parse(String.Format(@"
                                    {{
                                        ""Raven-Entity-Name"": ""{0}"", 
                                        ""Raven-Clr-Type"": ""{1}""
                                    }}", FailedMessageRetry.CollectionName, 
                    typeof(FailedMessageRetry).AssemblyQualifiedName))
                );
            return failureRetryId;
        }

        public void MoveBatchToStaging(string batchDocumentId, string[] failedMessageRetryIds)
        {
            try
            {
                Store.DatabaseCommands.Patch(batchDocumentId,
                    new[]
                    {
                        new PatchRequest
                        {
                            Type = PatchCommandType.Set, 
                            Name = "Status", 
                            Value = (int)RetryBatchStatus.Staging, 
                            PrevVal = (int)RetryBatchStatus.MarkingDocuments
                        }, 
                        new PatchRequest
                        {
                            Type = PatchCommandType.Set, 
                            Name = "FailureRetries", 
                            Value = new RavenJArray((IEnumerable)failedMessageRetryIds)
                        }
                    });
            }
            catch (ConcurrencyException)
            {
                // Ignore concurrency exceptions
            }
        }

        public void RemoveFailedMessageRetryDocument(string uniqueMessageId)
        {
            Store.DatabaseCommands.Delete(FailedMessageRetry.MakeDocumentId(uniqueMessageId), null);
        }

        internal bool AdoptOrphanedBatches()
        {
            using (var session = Store.OpenSession())
            {
                RavenQueryStatistics stats;

                var orphanedBatchIds = session.Query<RetryBatch, RetryBatches_ByStatusAndSession>()
                    .Where(b => b.Status == RetryBatchStatus.MarkingDocuments && b.RetrySessionId != RetrySessionId)
                    .Statistics(out stats)
                    .Select(b => b.Id)
                    .ToArray();

                AdoptBatches(session, orphanedBatchIds);

                var moreToDo = stats.IsStale || orphanedBatchIds.Any();
                return !moreToDo;
            }
        }

        void AdoptBatches(IDocumentSession session, string[] batchIds)
        {
            Parallel.ForEach(batchIds, batchId => AdoptBatch(session, batchId));
        }

        void AdoptBatch(IDocumentSession session, string batchId)
        {
            var query = session.Query<FailedMessageRetry, FailedMessageRetries_ByBatch>()
                .Where(r => r.RetryBatchId == batchId);

            var messageIds = new List<string>();

            using (var stream = session.Advanced.Stream(query))
            {
                while (stream.MoveNext())
                {
                    messageIds.Add(stream.Current.Document.Id);
                }
            }

            MoveBatchToStaging(batchId, messageIds.ToArray());
        }
    }
}