namespace ServiceControl.Recoverability.Retries
{
    using System;
    using System.Collections;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Indexes;
    using Raven.Client.Linq;
    using Raven.Database.Util;
    using Raven.Json.Linq;
    using ServiceControl.MessageFailures;

    public class Retryer
    {
        public IDocumentStore Store { get; set; }

        public void StartRetryForIndex<TIndex>(string batchId, Expression<Func<MessageFailureHistory, bool>> configure = null) where TIndex : AbstractIndexCreationTask, new()
        {
            var indexName = new TIndex().IndexName;
            StartRetryForIndex(batchId, indexName, configure);
        }

        public void StartRetryForIndex(string batchId, string indexName, Expression<Func<MessageFailureHistory, bool>> configure)
        {
            CreateBatch(batchId);

            MarkDocumentsInIndexAsPartOfBatch(batchId, indexName, configure)
                .ContinueWith(result =>
                {
                    MoveBatchToStaging(batchId, result.Result);
                }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        Task<string[]> MarkDocumentsInIndexAsPartOfBatch(string batchId, string indexName, Expression<Func<MessageFailureHistory,bool>> configure)
        {
            return Task<string[]>.Factory.StartNew(() =>
            {
                var failureRetryIds = new ConcurrentSet<string>();

                using (var session = Store.OpenSession())
                {
                    var qry = session.Query<MessageFailureHistory>(indexName);

                    if (configure != null)
                    {
                        qry = qry.Where(configure);
                    }

                    var page = 0;
                    var pageSize = 1000;
                    var skippedResults = 0;

                    while (true)
                    {
                        RavenQueryStatistics stats;
                        var ids = qry.Statistics(out stats)
                                    .Skip(page*pageSize + skippedResults)
                                    .Take(pageSize)
                                    .Select(x => x.UniqueMessageId)
                                    .ToArray();

                        if (!ids.Any())
                        {
                            break;
                        }

                        Parallel.ForEach(ids, id => failureRetryIds.Add(MakeFailureRetryDocument(batchId, id)));

                        page += 1;
                        skippedResults = stats.SkippedResults;
                    }
                }

                return failureRetryIds.ToArray();
            });
        }

        string MakeFailureRetryDocument(string batchId, string messageUniqueId)
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
                        Value = RetryBatch.MakeId(batchId)
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

        void MoveBatchToStaging(string batchId, string[] retryFailureIds)
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
                    }, 
                    new PatchRequest
                    {
                        Type = PatchCommandType.Set, 
                        Name = "FailureRetries", 
                        Value = new RavenJArray((IEnumerable)retryFailureIds)
                    }
                });
        }
    }
}
