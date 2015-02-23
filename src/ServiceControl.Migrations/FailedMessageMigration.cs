namespace ServiceControl.Migrations
{
    using System;
    using Raven.Client;

    public class FailedMessageMigration : ExpiredDocumentMigration<FailedMessage>
    {
        readonly FailedMessageToMessageFailureHistoryConverter historyConverter;
        readonly FailedMessageToMessageSnapshotDocumentConverter snapshotConverter;

        public FailedMessageMigration(IDocumentStore store, FailedMessageToMessageFailureHistoryConverter historyConverter, FailedMessageToMessageSnapshotDocumentConverter snapshotConverter) : base(store)
        {
            this.historyConverter = historyConverter;
            this.snapshotConverter = snapshotConverter;
        }

        public FailedMessageMigration(IDocumentStore store, FailedMessageToMessageFailureHistoryConverter historyConverter, FailedMessageToMessageSnapshotDocumentConverter snapshotConverter, TimeSpan timeToKeepMessagesBeforeExpiring, TimeSpan timerPeriod) 
            : base(store, timeToKeepMessagesBeforeExpiring, timerPeriod)
        {
            this.historyConverter = historyConverter;
            this.snapshotConverter = snapshotConverter;
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