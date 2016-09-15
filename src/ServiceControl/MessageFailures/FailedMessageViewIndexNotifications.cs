namespace ServiceControl.MessageFailures
{
    using System;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.MessageFailures.Api;

    class FailedMessageViewIndexNotifications : IObserver<IndexChangeNotification>
    {
        IBus bus;
        IDocumentStore store;
        int lastUnresolvedCount, lastArchivedCount;
        ILog logging = LogManager.GetLogger(typeof(FailedMessageViewIndexNotifications));

        public FailedMessageViewIndexNotifications(IDocumentStore store, IBus bus)
        {
            this.bus = bus;
            this.store = store;
        }

        public void OnNext(IndexChangeNotification value)
        {
            try
            {
                UpdatedCount();
            }
            catch (Exception ex)
            {
                logging.WarnFormat("Failed to emit MessageFailuresUpdated - {0}", ex);
            }
        }

        public void OnError(Exception error)
        {
            //Ignore  
        }

        public void OnCompleted()
        {
            //Ignore
        }

        void UpdatedCount()
        {
            using (var session = store.OpenSession())
            {
                var failedUnresolvedMessageCount = session.Query<FailedMessage, FailedMessageViewIndex>().Where(p => p.Status == FailedMessageStatus.Unresolved).CountLazily();
                var failedArchivedMessageCount = session.Query<FailedMessage, FailedMessageViewIndex>().Where(p => p.Status == FailedMessageStatus.Archived).CountLazily();
                
                session.Advanced.Eagerly.ExecuteAllPendingLazyOperations();

                if (lastUnresolvedCount == failedUnresolvedMessageCount.Value && lastArchivedCount == failedArchivedMessageCount.Value)
                {
                    return;
                }

                lastUnresolvedCount = failedUnresolvedMessageCount.Value;
                lastArchivedCount = failedArchivedMessageCount.Value;

                bus.Publish(new MessageFailuresUpdated
                {
                    Total = lastUnresolvedCount, // Left here for backwards compatibility, to be removed eventually.
                    UnresolvedTotal = lastUnresolvedCount,
                    ArchivedTotal = lastArchivedCount
                });
            }
        }
    }
}