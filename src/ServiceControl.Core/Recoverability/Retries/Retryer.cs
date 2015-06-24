namespace ServiceControl.Recoverability.Retries
{
    using System;
    using System.Threading.Tasks;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceControl.MessageFailures;

    public class Retryer
    {
        public IDocumentStore Store { get; set; }

        public string StartRetryForIndex(string indexName, string query = null)
        {
            var batchId = RetryBatch.MakeId(Guid.NewGuid().ToString());

            // TODO: Issue a message. Turn Batch into a Saga and let it handle it's state
            using (var session = Store.OpenSession())
            {
                session.Store(new RetryBatch
                {
                    Id = batchId,
                    Status = RetryBatchStatus.MarkingDocuments
                });
                session.SaveChanges();
            }

            var indexQueryText = String.Format("RetryId:[[NULL_VALUE]] AND Status:{0}", (int) FailedMessageStatus.Unresolved);
            if (query != null)
            {
                indexQueryText = indexQueryText + " AND " + query;
            }

            var operation = Store.DatabaseCommands.UpdateByIndex(indexName,
                new IndexQuery
                {
                    Query = indexQueryText
                },
                new[]
                {
                    new PatchRequest
                    {
                        Type = PatchCommandType.Set,
                        Name = "Status",
                        Value = (int) FailedMessageStatus.RetryIssued
                    }, 
                    new PatchRequest
                    {
                        Type = PatchCommandType.Set, 
                        Name = "RetryId", 
                        Value = batchId
                    }
                },
                allowStale: true);

            operation.WaitForCompletionAsync().ContinueWith(result =>
            {
                // TODO: Publish a message. Probably make the RetryBatch a saga and have it take care of it's status. 
                // That way if a shutdown happens we can issue this status change on a timer 
                using (var session = Store.OpenSession())
                {
                    var retryBatch = session.Load<RetryBatch>(batchId);
                    retryBatch.Status = RetryBatchStatus.Staging;
                    session.SaveChanges();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion);

            return batchId;
        }
    }
}
