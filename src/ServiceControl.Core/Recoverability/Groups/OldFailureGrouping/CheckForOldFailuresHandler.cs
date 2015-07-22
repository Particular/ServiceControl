namespace ServiceControl.Recoverability.Groups.OldFailureGrouping
{
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceControl.Recoverability.Groups.Indexes;
    
    public class CheckForOldFailuresHandler : IHandleMessages<CheckForOldFailures>
    {
        const int BatchSize = 1;

        IBus bus;
        IDocumentSession session;
        MessageFailureHistoryGrouper grouper;
        static readonly ILog Logger = LogManager.GetLogger(typeof(GroupOldFailuresHandler));


        public CheckForOldFailuresHandler(IBus bus, IDocumentSession session, MessageFailureHistoryGrouper grouper)
        {
            this.bus = bus;
            this.session = session;
            this.grouper = grouper;
        }

        public void Handle(CheckForOldFailures message)
        {
            var grouperSetId = grouper.GetGrouperSetId();
            var batchDocumentId = GroupOldFailureBatch.MakeDocumentId(grouperSetId);
            
            var existingBatch = session.Load<GroupOldFailureBatch>(batchDocumentId);
            if (existingBatch != null)
            {
                return;
            }
            
            var failureIds = GetIncorrectlyGroupedFailures(grouperSetId);
            if (failureIds.Count == 0)
            {
                return;
            }

            var batch = CreateFailureBatch(failureIds, batchDocumentId);
            
            session.Store(batch);

            session.SaveChanges();

            bus.SendLocal<GroupOldFailures>(g => { g.BatchId = batchDocumentId; });
        }

        static GroupOldFailureBatch CreateFailureBatch(List<string> failureIds, string batchDocumentId)
        {
            var ids = Batch(failureIds, BatchSize);
            var batch = new GroupOldFailureBatch
            {
                Id = batchDocumentId,
                Failures = ids.Select(t => t.ToList()).ToList()
            };
            return batch;
        }

        List<string> GetIncorrectlyGroupedFailures(string grouperSetId)
        {
            var query = session.Query<MessageFailuresByGroupSet, MessageFailuresByGrouperSetIndex>()
                .Where(f => f.GrouperSetId != grouperSetId);

            var ids = new List<string>();

            using (var stream = session.Advanced.Stream(query))
            {
                while (stream.MoveNext())
                {
                    ids.AddRange(stream.Current.Document.MessageIds);
                }
            }

            return ids;
        }

        static IEnumerable<IEnumerable<T>> Batch<T>(IEnumerable<T> collection, int batchSize)
        {
            var nextbatch = new List<T>(batchSize);
            foreach (T item in collection)
            {
                nextbatch.Add(item);
                if (nextbatch.Count == batchSize)
                {
                    yield return nextbatch;
                    nextbatch = new List<T>(batchSize);
                }
            }
            if (nextbatch.Count > 0)
                yield return nextbatch;
        }
    }
}
