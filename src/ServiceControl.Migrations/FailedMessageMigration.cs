namespace ServiceControl.Migrations
{
    using System;
    using Raven.Client;

    public class FailedMessageMigration : ExpiredDocumentMigration<FailedMessage>
    {
        readonly FailedMessageToMessageFailureHistoryConverter historyConverter = new FailedMessageToMessageFailureHistoryConverter();
        readonly FailedMessageToMessageSnapshotDocumentConverter snapshotConverter = new FailedMessageToMessageSnapshotDocumentConverter();

        public FailedMessageMigration(IDocumentStore store) 
            : base(store)
        {
        }

        public FailedMessageMigration(IDocumentStore store, TimeSpan timeToKeepMessagesBeforeExpiring) 
            : base(store, timeToKeepMessagesBeforeExpiring)
        {
        }

        protected override string EntityName
        {
            get { return "FailedMessages"; }
        }

        protected override void Migrate(FailedMessage document, IDocumentSession updateSession, DateTime expiryDate, Func<bool> shouldCancel)
        {
            var historyDoc = historyConverter.Convert(document);
            updateSession.Store(historyDoc);

            var snapshotDoc = snapshotConverter.Convert(document);
            if (snapshotDoc.AttemptedAt > expiryDate)
            {
                updateSession.Store(snapshotDoc);
            }
        }
    }
}