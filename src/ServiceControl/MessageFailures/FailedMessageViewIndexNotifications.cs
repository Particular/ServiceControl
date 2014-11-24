namespace ServiceControl.MessageFailures
{
    using System;
    using System.Linq;
    using NServiceBus;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;
    using ServiceControl.Contracts.MessageFailures;
    using INeedInitialization = NServiceBus.INeedInitialization;


    public class FailedMessageViewIndexNotifications : INeedInitialization, IObserver<IndexChangeNotification>
    {
        IDocumentStore Store;
        IBus Bus;

        const string failedMsgsIndex = "FailedMessageViewIndex";

        public FailedMessageViewIndexNotifications()
        {
            // Need this because INeedInitialization does not use DI instead use Activator.CreateInstance
        }

        public FailedMessageViewIndexNotifications(IDocumentStore store, IBus bus)
        {
            Bus = bus;
            Store = store;
            UpdatedCount();

            var s = store.DatabaseCommands.GetIndexes(0, 128).Select(x => x.Name).ToList();
            if (s.Contains(failedMsgsIndex))
            {
                store.Changes().ForIndex(failedMsgsIndex).Subscribe(this);
            }
            else
            {
                throw new IndexDoesNotExistsException(string.Format("Can't find {0} index", failedMsgsIndex));
            }
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
            UpdatedCount();
        }

        void UpdatedCount()
        {
            using (var session = Store.OpenSession())
            {
                var total = session.Query<FailedMessage>().Count(m => m.Status == FailedMessageStatus.Unresolved);
                Bus.Publish(new MessageFailuresUpdated { Total = total });
            }
        }

        public void Init()
        {
            Configure.Component<FailedMessageViewIndexNotifications>(DependencyLifecycle.SingleInstance);
        }
    }
}