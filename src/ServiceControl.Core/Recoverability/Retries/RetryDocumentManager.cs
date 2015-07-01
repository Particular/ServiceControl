namespace ServiceControl.Recoverability.Retries
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.IdGeneration;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Indexes;
    using Raven.Client.Linq;
    using Raven.Json.Linq;
    using ServiceControl.MessageFailures;

    public class RetryDocumentManager
    {
        public IDocumentStore Store { get; set; }

        static string RetrySessionId = CombGuid.Generate().ToString();

        public string MakeFailureRetryDocument(string batchDocumentId, string messageUniqueId)
        {
            var failureRetryId = MessageFailureRetry.MakeDocumentId(messageUniqueId);
            Store.DatabaseCommands.Patch(failureRetryId,
                new PatchRequest[0],
                new[]
                {
                    new PatchRequest
                    {
                        Name = "FailureMessageId",
                        Type = PatchCommandType.Set,
                        Value = MessageFailureHistory.MakeDocumentId(messageUniqueId)
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
                                    }}", MessageFailureRetry.CollectionName,
                    typeof(MessageFailureRetry).AssemblyQualifiedName))
                );

            return failureRetryId;
        }

        public void RemoveFailureRetryDocument(string uniqueMessageId)
        {
            Store.DatabaseCommands.Delete(MessageFailureRetry.MakeDocumentId(uniqueMessageId), null);
        }

        public void CreateBatch(string batchDocumentId)
        {
            using (var session = Store.OpenSession())
            {
                session.Store(new RetryBatch
                {
                    Id = batchDocumentId,
                    RetrySessionId = RetrySessionId,
                    Status = RetryBatchStatus.MarkingDocuments
                });
                session.SaveChanges();
            }
        }

        public void MoveBatchToStaging(string batchDocumentId, string[] retryFailureIds)
        {
            Store.DatabaseCommands.Patch(batchDocumentId,
                new[]
                {
                    new PatchRequest
                    {
                        Type = PatchCommandType.Set,
                        Name = "Status",
                        Value = (int) RetryBatchStatus.Staging,
                        PrevVal = (int) RetryBatchStatus.MarkingDocuments
                    }, 
                    new PatchRequest
                    {
                        Type = PatchCommandType.Set, 
                        Name = "FailureRetries", 
                        Value = new RavenJArray((IEnumerable)retryFailureIds)
                    }
                });
        }

        public void AdoptOrphanedBatches()
        {
            using (var session = Store.OpenSession())
            {
                var batches = session.Query<RetryBatch, RetryBatches_ByStatusAndSession>()
                    .Customize(q => q.WaitForNonStaleResultsAsOfNow())
                    .Where(b => b.Status == RetryBatchStatus.MarkingDocuments && b.RetrySessionId != RetrySessionId)
                    .ToArray();

                AdoptBatches(session, batches.Select(x => x.Id));
            }
        }

        private void AdoptBatches(IDocumentSession session, IEnumerable<string> batchIds)
        {
            Parallel.ForEach(batchIds, batchId => AdoptBatch(session, batchId));
        }

        private void AdoptBatch(IDocumentSession session, string batchId)
        {
            var query = session.Query<MessageFailureRetry, MessageFailureRetries_ByBatch>()
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

    public class RetryBatches_ByStatusAndSession : AbstractIndexCreationTask<RetryBatch>
    {
        public RetryBatches_ByStatusAndSession()
        {
            Map = docs => from doc in docs
                select new
                {
                    doc.RetrySessionId,
                    doc.Status 
                };
        }
    }

    public class MessageFailureRetries_ByBatch : AbstractIndexCreationTask<MessageFailureRetry>
    {
        public MessageFailureRetries_ByBatch()
        {
            Map = docs => from doc in docs
                select new
                {
                    doc.RetryBatchId
                };
        }
    }
}