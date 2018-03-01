namespace ServiceControl.MessageFailures
{
    using System;
    using System.Linq;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.MessageFailures.Api;

    class FailedMessageViewIndexNotifications : IObserver<IndexChangeNotification>
    {
        IDocumentStore store;
        IDomainEvents domainEvents;
        int lastUnresolvedCount, lastArchivedCount;
        ILog logging = LogManager.GetLogger(typeof(FailedMessageViewIndexNotifications));

        public FailedMessageViewIndexNotifications(IDocumentStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
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
                var failedUnresolvedMessageCount = session.Query<FailedMessage, FailedMessageViewIndex>().Count(p => p.Status == FailedMessageStatus.Unresolved);
                var failedArchivedMessageCount = session.Query<FailedMessage, FailedMessageViewIndex>().Count(p => p.Status == FailedMessageStatus.Archived);

                if (lastUnresolvedCount == failedUnresolvedMessageCount && lastArchivedCount == failedArchivedMessageCount)
                {
                    return;
                }
                lastUnresolvedCount = failedUnresolvedMessageCount;
                lastArchivedCount = failedArchivedMessageCount;

                domainEvents.Raise(new MessageFailuresUpdated
                {
                    Total = failedUnresolvedMessageCount, // Left here for backwards compatibility, to be removed eventually.
                    UnresolvedTotal = failedUnresolvedMessageCount,
                    ArchivedTotal = failedArchivedMessageCount
                });
            }
        }
    }
}