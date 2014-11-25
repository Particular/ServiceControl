namespace ServiceControl.MessageFailures
{
    using System;
    using System.Linq;
    using NServiceBus;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using INeedInitialization = NServiceBus.INeedInitialization;

    public class FailedMessageViewIndexNotifications : INeedInitialization, IWantToRunWhenBusStartsAndStops, IObserver<IndexChangeNotification>
    {
        IBus bus;
        IDocumentStore store;
        int lastCount;

        public FailedMessageViewIndexNotifications()
        {
            // Need this because INeedInitialization does not use DI instead use Activator.CreateInstance
        }

        public FailedMessageViewIndexNotifications(IDocumentStore store, IBus bus)
        {
            this.bus = bus;
            this.store = store;
        }

        public void OnNext(IndexChangeNotification value)
        {
            UpdatedCount();
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
                bus.Publish(new MessageFailuresUpdated { Total = failedMessageCount });
            }
        }

        public void Init()
        {
            Configure.Component<FailedMessageViewIndexNotifications>(DependencyLifecycle.SingleInstance);
        }

        public void Start()
        {
            store.Changes().ForIndex("FailedMessageViewIndex").Subscribe(this);
        }

        public void Stop()
        {
            //Ignore
        }
    }
}