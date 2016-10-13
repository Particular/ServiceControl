namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;
    using Raven.Client.Linq;
    using Raven.Json.Linq;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class RetryDocumentManager
    {
        public IDocumentStore Store { get; set; }
        public RetryOperationManager RetryOperationManager { get; set; }

        static string RetrySessionId = Guid.NewGuid().ToString();

        private bool abort;

        public RetryDocumentManager(ShutdownNotifier notifier)
        {
            notifier.Register(() => { abort = true; });
        }

        public string CreateBatchDocument(string requestId, RetryType retryType, int initialBatchSize, string context = null)
        {
            var batchDocumentId = RetryBatch.MakeDocumentId(Guid.NewGuid().ToString());
            using (var session = Store.OpenSession())
            {
                session.Store(new RetryBatch
                {
                    Id = batchDocumentId,
                    Context = context,
                    RequestId = requestId,
                    RetryType = retryType,
                    InitialBatchSize = initialBatchSize,
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
                RavenJObject.Parse($@"
                                    {{
                                        ""Raven-Entity-Name"": ""{FailedMessageRetry.CollectionName}"", 
                                        ""Raven-Clr-Type"": ""{typeof(FailedMessageRetry).AssemblyQualifiedName}""
                                    }}")
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
                log.DebugFormat("Ignoring concurrency exception while moving batch to staging {0}", batchDocumentId);
            }
        }

        public void RemoveFailedMessageRetryDocument(string uniqueMessageId)
        {
            Store.DatabaseCommands.Delete(FailedMessageRetry.MakeDocumentId(uniqueMessageId), null);
        }

        internal void AdoptOrphanedBatches(IDocumentSession session, out bool hasMoreWorkToDo)
        {
            RavenQueryStatistics stats;

            var orphanedBatchIds = session.Query<RetryBatch, RetryBatches_ByStatusAndSession>()
                .Where(b => b.Status == RetryBatchStatus.MarkingDocuments && b.RetrySessionId != RetrySessionId)
                .Statistics(out stats)
                .Select(b => b.Id)
                .ToArray();

            log.InfoFormat("Found {0} orphaned retry batches from previous sessions", orphanedBatchIds.Length);

            AdoptBatches(session, orphanedBatchIds);

            if (abort)
            {
                hasMoreWorkToDo = false;
                return;
            }

            hasMoreWorkToDo = stats.IsStale || orphanedBatchIds.Any();
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
                while (!abort && stream.MoveNext())
                {
                    messageIds.Add(stream.Current.Document.Id);
                }
            }

            if (!abort)
            {
                log.InfoFormat("Adopting retry batch {0} from previous session with {1} messages", batchId, messageIds.Count);
                MoveBatchToStaging(batchId, messageIds.ToArray());
            }
        }

        internal void RebuildRetryOperationState(IDocumentSession session)
        {
            var stagingBatchGroups = session.Query<RetryBatch>()
                .Customize(q => q.Include<RetryBatch, FailedMessageRetry>(b => b.FailureRetries))
                .Where(b => b.Status == RetryBatchStatus.Staging)
                .ToArray()
                .GroupBy(batch => new { RetryType = batch.RetryType, RequestId = batch.RequestId });


            foreach (var group in stagingBatchGroups)
            {
                foreach (var batch in group)
                {
                    if (!string.IsNullOrWhiteSpace(batch.RequestId))
                    {
                        RetryOperationManager.SetInProgress(batch.RequestId, batch.RetryType, group.Sum(g => g.InitialBatchSize));
                    }
                }
            }
        }

        static ILog log = LogManager.GetLogger(typeof(RetryDocumentManager));
    }
}