namespace ServiceControl.Recoverability.Groups.OldFailureGrouping
{
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceControl.MessageFailures;

    public class GroupOldFailuresHandler : IHandleMessages<GroupOldFailures>
    {
        IBus bus;
        IDocumentSession session;
        MessageFailureHistoryGrouper grouper;
        static readonly ILog Logger = LogManager.GetLogger(typeof(GroupOldFailuresHandler));


        public GroupOldFailuresHandler(IBus bus, IDocumentSession session, MessageFailureHistoryGrouper grouper)
        {
            this.bus = bus;
            this.session = session;
            this.grouper = grouper;
        }

        public void Handle(GroupOldFailures message)
        {
            var batchDocument = session.Load<GroupOldFailureBatch>(message.BatchId);
            if (batchDocument == null)
            {
                return;
            }

            if (batchDocument.ContainsMoreBatches() == false)
            {
                return;
            }

            var batch = batchDocument.ConsumeBatch();

            var failures = session.Load<MessageFailureHistory>(batch.Select(MessageFailureHistory.MakeDocumentId).ToArray());

            Logger.InfoFormat("Grouping batch of {0} message failure(s).", failures.Length);

            foreach (var failure in failures)
            {
                grouper.Group(failure);
            }
            session.SaveChanges();

            Logger.InfoFormat("Grouped {0} message failure(s) successfully.", failures.Length);

            if (batchDocument.ContainsMoreBatches())
            {
                bus.SendLocal<GroupOldFailures>(g => { g.BatchId = message.BatchId; });
            }
        }
    }
}
