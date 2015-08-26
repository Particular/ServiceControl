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
        int lastCount;
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
                var failedMessageCount = session.Query<FailedMessage, FailedMessageViewIndex>().Count(p => p.Status == FailedMessageStatus.Unresolved);
                if (lastCount == failedMessageCount)
                    return;
                lastCount = failedMessageCount;
                bus.Publish(new MessageFailuresUpdated
                {
                    Total = failedMessageCount
                });
            }
        }
    }
}