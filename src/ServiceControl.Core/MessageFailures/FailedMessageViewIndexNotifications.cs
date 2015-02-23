namespace ServiceControl.MessageFailures
{
    using System;
    using System.Linq;
    using System.Reactive.Concurrency;
    using System.Reactive.Linq;
    using NServiceBus;
    using NServiceBus.Logging;
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
        IDisposable subscription;
        ILog logging = LogManager.GetLogger(typeof(FailedMessageViewIndexNotifications));
        
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
            try
            {
                using (var session = store.OpenSession())
                {
                    var failedMessageCount = session.Query<MessageFailureHistory, FailedMessageViewIndex>().Count(p => p.Status == FailedMessageStatus.Unresolved);
                    if (lastCount == failedMessageCount)
                        return;
                    lastCount = failedMessageCount;
                    bus.Publish(new MessageFailuresUpdated
                    {
                        Total = failedMessageCount
                    });
                }
            }
            catch(Exception ex)
            {
                logging.WarnFormat("Failed to emit MessageFailuresUpdated - {0}", ex);
            }
        }

        public void Init()
        {
            Configure.Component<FailedMessageViewIndexNotifications>(DependencyLifecycle.SingleInstance);
        }

        public void Start()
        {
            subscription = store.Changes().ForIndex("FailedMessageViewIndex").SubscribeOn(Scheduler.Default).Subscribe(this);
        }

        public void Stop()
        {
            subscription.Dispose();
        }
    }
}