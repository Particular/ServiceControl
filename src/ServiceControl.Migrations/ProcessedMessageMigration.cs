namespace ServiceControl.Migrations
{
    using System;
    using Raven.Client;

    public class ProcessedMessageMigration : ExpiredDocumentMigration<ProcessedMessage>
    {
        readonly ProcessedMessageConverter converter = new ProcessedMessageConverter();

        public ProcessedMessageMigration(IDocumentStore store) : base(store)
        {
        }

        public ProcessedMessageMigration(IDocumentStore store, TimeSpan timeToKeepMessagesBeforeExpiring) : base(store, timeToKeepMessagesBeforeExpiring)
        {
        }

        protected override string EntityName
        {
            get { return "ProcessedMessages"; }
        }

        protected override void Migrate(ProcessedMessage document, IDocumentSession updateSession, DateTime expiryDate, Func<bool> shouldCancel)
        {
            var snapshotDoc = converter.Convert(document);
            if (snapshotDoc.AttemptedAt > expiryDate)
            {
                updateSession.Store(snapshotDoc);
            }
        }
    }
}