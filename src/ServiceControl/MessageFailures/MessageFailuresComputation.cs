namespace ServiceControl.MessageFailures
{
    using System.Linq;
    using System.Threading;
    using NServiceBus;
    using Raven.Client;

    public class MessageFailuresComputation : INeedInitialization
    {
        int unresolvedFailures;

        public MessageFailuresComputation()
        {
            // Need this because INeedInitialization does not use DI instead use Activator.CreateInstance
        }

        public MessageFailuresComputation(IDocumentStore store)
        {
            using (var session = store.OpenSession())
            {
                unresolvedFailures = session.Query<FailedMessage>().Count(m => m.Status == FailedMessageStatus.Unresolved);
            }
        }

        public int MessageResolved()
        {
            return Interlocked.Decrement(ref unresolvedFailures);
        }

        public int MessageFailed()
        {
            return Interlocked.Increment(ref unresolvedFailures);
        }

        public void Init()
        {
            Configure.Component<MessageFailuresComputation>(DependencyLifecycle.SingleInstance);
        }
    }
}