namespace ServiceControl.MessageFailures
{
    using System;
    using System.Threading.Tasks;
    using Api;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Client;

    class FailedMessageViewIndexNotifications : IObserver<IndexChangeNotification>
    {
        public FailedMessageViewIndexNotifications(IDocumentStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
        }

        public void OnNext(IndexChangeNotification value)
        {
            try
            {
                UpdatedCount().GetAwaiter().GetResult();
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

        async Task UpdatedCount()
        {
            using (var session = store.OpenAsyncSession())
            {
                var failedUnresolvedMessageCount = await session.Query<FailedMessage, FailedMessageViewIndex>().CountAsync(p => p.Status == FailedMessageStatus.Unresolved)
                    .ConfigureAwait(false);
                var failedArchivedMessageCount = await session.Query<FailedMessage, FailedMessageViewIndex>().CountAsync(p => p.Status == FailedMessageStatus.Archived)
                    .ConfigureAwait(false);

                if (lastUnresolvedCount == failedUnresolvedMessageCount && lastArchivedCount == failedArchivedMessageCount)
                {
                    return;
                }

                lastUnresolvedCount = failedUnresolvedMessageCount;
                lastArchivedCount = failedArchivedMessageCount;

                await domainEvents.Raise(new MessageFailuresUpdated
                {
                    Total = failedUnresolvedMessageCount, // Left here for backwards compatibility, to be removed eventually.
                    UnresolvedTotal = failedUnresolvedMessageCount,
                    ArchivedTotal = failedArchivedMessageCount
                }).ConfigureAwait(false);
            }
        }

        IDocumentStore store;
        IDomainEvents domainEvents;
        int lastUnresolvedCount, lastArchivedCount;
        ILog logging = LogManager.GetLogger(typeof(FailedMessageViewIndexNotifications));
    }
}