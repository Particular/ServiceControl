namespace ServiceControl.Recoverability.Retries
{
    using System;
    using System.Collections;
    using System.Linq;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Linq;
    using Raven.Json.Linq;
    using ServiceControl.MessageFailures;

    public class RetryDocumentManager
    {
        public IDocumentStore Store { get; set; }

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
                var batches = session.Query<RetryBatch>()
                    .Customize(q => q.WaitForNonStaleResultsAsOfNow())
                    .Where(b => b.Status == RetryBatchStatus.MarkingDocuments)
                    .ToArray();

                foreach (var batch in batches)
                {
                    var batchId = batch.Id;
                    var retryFailureIds = session.Query<MessageFailureRetry>()
                        .Customize(q => q.WaitForNonStaleResultsAsOfNow())
                        .Where(r => r.RetryBatchId == batchId)
                        .Select(r => r.Id)
                        .ToArray();

                    MoveBatchToStaging(batch.Id, retryFailureIds);
                }
            }
        }
    }
}