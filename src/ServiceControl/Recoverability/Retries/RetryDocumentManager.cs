namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;
    using Raven.Client.Linq;
    using Raven.Json.Linq;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class RetryDocumentManager
    {
        private static string RetrySessionId = Guid.NewGuid().ToString();
        private static RavenJObject defaultMetadata = RavenJObject.Parse($@"
                                    {{
                                        ""Raven-Entity-Name"": ""{FailedMessageRetry.CollectionName}"", 
                                        ""Raven-Clr-Type"": ""{typeof(FailedMessageRetry).AssemblyQualifiedName}""
                                    }}");
        private static PatchRequest[] patchRequestsEmpty = new PatchRequest[0];

        private IDocumentStore store;
        private bool abort;

        public RetryDocumentManager(ShutdownNotifier notifier, IDocumentStore store)
        {
            this.store = store;
            notifier.Register(() => { abort = true; });
        }

        public string CreateBatchDocument(string context = null, string groupId = null, int? totalRetryBatchesInGroup = null)
        {
            var batchDocumentId = RetryBatch.MakeDocumentId(Guid.NewGuid().ToString());
            using (var session = store.OpenSession())
            {
                session.Store(new RetryBatch
                {
                    Id = batchDocumentId, 
                    Context = context,
                    GroupId = groupId,
                    TotalRetryBatchesInGroup = totalRetryBatchesInGroup,
                    RetrySessionId = RetrySessionId, 
                    Status = RetryBatchStatus.MarkingDocuments
                });
                session.SaveChanges();
            }
            return batchDocumentId;
        }

        public ICommandData CreateFailedMessageRetryDocument(string batchDocumentId, string messageUniqueId)
        {
            var failureRetryId = FailedMessageRetry.MakeDocumentId(messageUniqueId);

            return new PatchCommandData
            {
                Patches = patchRequestsEmpty,
                PatchesIfMissing = new[]
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
                Key = failureRetryId,
                Metadata = defaultMetadata
            };
        }

        public void MoveBatchToStaging(string batchDocumentId, string[] failedMessageRetryIds)
        {
            try
            {
                store.DatabaseCommands.Patch(batchDocumentId,
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
            store.DatabaseCommands.Delete(FailedMessageRetry.MakeDocumentId(uniqueMessageId), null);
        }

        internal void AdoptOrphanedBatches(IDocumentSession session, out bool hasMoreWorkToDo)
        {
            using (var session = store.OpenSession())
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

        internal void RebuildRetryGroupState(IDocumentSession session)
        {
            var stagingBatches = session.Query<RetryBatch>()
                .Customize(q => q.Include<RetryBatch, FailedMessageRetry>(b => b.FailureRetries))
                .Where(b => b.Status == RetryBatchStatus.Staging);

            foreach (var batch in stagingBatches)
            {
                if (!string.IsNullOrWhiteSpace(batch.GroupId))
                {
                    var numberOfBatches = batch.TotalRetryBatchesInGroup ?? 1;
                    // Todo: can we include this query as part of the query above to prevent Select N+1?
                    var numberOfIncompleteBatchesForGroup = stagingBatches.Where(b => b.GroupId == batch.GroupId).Count();

                    RetryGroupSummary.SetStatus(batch.GroupId, RetryGroupStatus.Staging, numberOfBatches - numberOfIncompleteBatchesForGroup, numberOfBatches);
                }
            }

            var batchReadyForForwarding = session.Include<RetryBatchNowForwarding, RetryBatch>(r => r.RetryBatchId)
                .Load<RetryBatchNowForwarding>(RetryBatchNowForwarding.Id);

            if (batchReadyForForwarding != null)
            {
                var forwardingBatch = session.Load<RetryBatch>(batchReadyForForwarding.RetryBatchId);

                if (forwardingBatch != null)
                {
                    var numberOfBatches = forwardingBatch.TotalRetryBatchesInGroup ?? 1;
                    // Todo: can we include this query as part of the query above to prevent Select N+1?
                    var numberOfIncompleteBatchesForGroup = stagingBatches.Where(b => b.GroupId == forwardingBatch.GroupId).Count();

                    RetryGroupSummary.SetStatus(forwardingBatch.GroupId, RetryGroupStatus.Forwarding, numberOfBatches - numberOfIncompleteBatchesForGroup, numberOfBatches);
                }
            }
        }

        static ILog log = LogManager.GetLogger(typeof(RetryDocumentManager));
    }
}