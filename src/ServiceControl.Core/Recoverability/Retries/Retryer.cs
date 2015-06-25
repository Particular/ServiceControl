namespace ServiceControl.Recoverability.Retries
{
    using System;
    using System.Threading.Tasks;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Indexes;
    using ServiceControl.MessageFailures;

    public class Retryer
    {
        public IDocumentStore Store { get; set; }

        public void StartRetryForIndex<TIndex>(string batchId, string query = null) where TIndex : AbstractIndexCreationTask , new()
        {
            var indexName = new TIndex().IndexName;
            StartRetryForIndex(batchId, indexName, query);
        }

        public void StartRetryForIndex(string batchId, string indexName, string query = null)
        {
            CreateBatch(batchId);

            MarkDocumentsInIndexAsPartOfBatch(batchId, indexName, query)
                .ContinueWith(result =>
                {
                    MoveBatchToStaging(batchId);
                }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        Task MarkDocumentsInIndexAsPartOfBatch(string batchId, string indexName, string query)
        {
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
                false);
            return operation.WaitForCompletionAsync();
        }

        void CreateBatch(string batchId)
        {
            using (var session = Store.OpenSession())
            {
                session.Store(new RetryBatch
                {
                    Id = RetryBatch.MakeId(batchId), 
                    Status = RetryBatchStatus.MarkingDocuments, 
                    Started = DateTimeOffset.UtcNow
                });
                session.SaveChanges();
            }
        }

        void MoveBatchToStaging(string batchId)
        {
            Store.DatabaseCommands.Patch(RetryBatch.MakeId(batchId),
                new[]
                {
                    new PatchRequest
                    {
                        Type = PatchCommandType.Set,
                        Name = "Status",
                        Value = (int) RetryBatchStatus.Staging,
                        PrevVal = (int) RetryBatchStatus.MarkingDocuments
                    }
                });
        }
    }
}
