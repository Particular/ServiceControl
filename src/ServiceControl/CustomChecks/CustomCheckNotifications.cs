namespace ServiceControl.MessageFailures
{
    using System;
    using System.Linq;
    using NServiceBus;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceControl.CustomChecks;
    using INeedInitialization = NServiceBus.INeedInitialization;

    public class CustomCheckNotifications : INeedInitialization, IWantToRunWhenBusStartsAndStops, IObserver<IndexChangeNotification>
    {
        IDocumentStore store;
        IBus bus;
        int lastCount;

        public CustomCheckNotifications()
        {
            // Need this because INeedInitialization does not use DI instead use Activator.CreateInstance
        }

        public CustomCheckNotifications(IDocumentStore store, IBus bus)
        {
            this.bus = bus;
            this.store = store;
        }

        public void Init()
        {
            Configure.Component<CustomCheckNotifications>(DependencyLifecycle.SingleInstance);
        }

        public void OnNext(IndexChangeNotification value)
        {
           UpdateCount();
        }

        void UpdateCount()
        {
            using (var session = store.OpenSession())
            {
                var failedCustomCheckCount = session.Query<CustomCheck, CustomChecksIndex>().Count(p => p.Status == Status.Fail);
                if (lastCount == failedCustomCheckCount)
                    return;
                lastCount = failedCustomCheckCount;
                bus.Publish(new CustomChecksUpdated { Failed = lastCount });
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

        public void Start()
        {
            store.Changes().ForIndex("CustomChecksIndex").Subscribe(this);
        }

        public void Stop()
        {
            //Ignore
        }
    }
}