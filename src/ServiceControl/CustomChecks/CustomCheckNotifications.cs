namespace ServiceControl.CustomChecks
{
    using System;
    using System.Linq;
    using System.Reactive.Concurrency;
    using System.Reactive.Linq;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using INeedInitialization = NServiceBus.INeedInitialization;

    public class CustomCheckNotifications : INeedInitialization, IWantToRunWhenBusStartsAndStops, IObserver<IndexChangeNotification>
    {
        IDocumentStore store;
        IBus bus;
        int lastCount;
        IDisposable subscription;
        ILog logging = LogManager.GetLogger(typeof(CustomCheckNotifications));
    
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
            try
            {
                using (var session = store.OpenSession())
                {
                    var failedCustomCheckCount = session.Query<CustomCheck, CustomChecksIndex>().Count(p => p.Status == Status.Fail);
                    if (lastCount == failedCustomCheckCount)
                        return;
                    lastCount = failedCustomCheckCount;
                    bus.Publish(new CustomChecksUpdated
                    {
                        Failed = lastCount
                    });
                }
            }
            catch (Exception ex)
            {
                logging.WarnFormat("Failed to emit CustomCheckUpdated - {0}", ex);
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
            subscription = store.Changes().ForIndex("CustomChecksIndex").SubscribeOn(Scheduler.Default).Subscribe(this);
        }

        public void Stop()
        {
            subscription.Dispose();
        }
    }
}