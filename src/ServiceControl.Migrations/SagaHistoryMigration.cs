namespace ServiceControl.Migrations
{
    using System;
    using System.Linq;
    using Particular.Backend.Debugging.RavenDB.Model;
    using Raven.Client;

    public class SagaHistoryMigration : ExpiredDocumentMigration<SagaHistory>
    {
        public SagaHistoryMigration(IDocumentStore store) : base(store)
        {
        }

        public SagaHistoryMigration(IDocumentStore store, TimeSpan timeToKeepMessagesBeforeExpiring) 
            : base(store, timeToKeepMessagesBeforeExpiring)
        {
        }

        protected override void Migrate(SagaHistory document, IDocumentSession updateSession, DateTime expiryDate, Func<bool> shouldCancel)
        {
            foreach (var sagaStateChange in document.Changes
                .Where(x => x.FinishTime > expiryDate))
            {
                if (shouldCancel())
                {
                    return;
                }
                updateSession.Store(ConvertToSnapshot(document, sagaStateChange));
            }
        }

        static SagaSnapshot ConvertToSnapshot(SagaHistory sagaHistory, SagaStateChange sagaStateChange)
        {
            return new SagaSnapshot
                   {
                       SagaId = sagaHistory.SagaId,
                       Endpoint = sagaStateChange.Endpoint,
                       FinishTime = sagaStateChange.FinishTime,
                       InitiatingMessage = sagaStateChange.InitiatingMessage,
                       OutgoingMessages = sagaStateChange.OutgoingMessages,
                       SagaType = sagaHistory.SagaType,
                       StartTime = sagaStateChange.StartTime,
                       StateAfterChange = sagaStateChange.StateAfterChange,
                       Status = sagaStateChange.Status,
                   };
        }

        protected override string EntityName
        {
            get { return "SagaHistories"; }
        }
    }

}
