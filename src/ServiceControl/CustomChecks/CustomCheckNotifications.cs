namespace ServiceControl.CustomChecks
{
    using System;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Client;

    class CustomCheckNotifications : IObserver<IndexChangeNotification>
    {
        public CustomCheckNotifications(IDocumentStore store, IBusSession busSession)
        {
            this.busSession = busSession;
            this.store = store;
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
                busSession.Publish(new CustomChecksUpdated
                {
                    Failed = lastCount
                }).GetAwaiter().GetResult();
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

        IBusSession busSession;
        IDocumentStore store;
        int lastCount;
        ILog logging = LogManager.GetLogger(typeof(CustomCheckNotifications));
    }
}