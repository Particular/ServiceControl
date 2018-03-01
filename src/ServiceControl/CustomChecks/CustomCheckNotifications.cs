namespace ServiceControl.CustomChecks
{
    using System;
    using System.Linq;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceControl.Infrastructure.DomainEvents;

    class CustomCheckNotifications : IObserver<IndexChangeNotification>
    {
        public CustomCheckNotifications(IDocumentStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
        }

        public void OnNext(IndexChangeNotification value)
        {
            try
            {
                UpdateCount();
            }
            catch (Exception ex)
            {
                logging.WarnFormat("Failed to emit CustomCheckUpdated - {0}", ex);
            }
        }

        void UpdateCount()
        {
            using (var session = store.OpenSession())
            {
                var failedCustomCheckCount = session.Query<CustomCheck, CustomChecksIndex>().Count(p => p.Status == Status.Fail);
                if (lastCount == failedCustomCheckCount)
                    return;
                lastCount = failedCustomCheckCount;
                domainEvents.Raise(new CustomChecksUpdated
                {
                    Failed = lastCount
                });
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

        IDomainEvents domainEvents;
        IDocumentStore store;
        int lastCount;
        ILog logging = LogManager.GetLogger(typeof(CustomCheckNotifications));
    }
}