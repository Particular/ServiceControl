namespace ServiceControl.CustomChecks
{
    using System.Linq;
    using System.Threading;
    using NServiceBus;
    using Raven.Client;

    public class CustomChecksComputation : INeedInitialization
    {
        public CustomChecksComputation(IDocumentStore store)
        {
            using (var session = store.OpenSession())
            {
                totalFailures = session.Query<CustomCheck>().Count(c => c.Status == Status.Fail);
            }
        }

        public void Init()
        {
            Configure.Component<CustomChecksComputation>(DependencyLifecycle.SingleInstance);
        }

        public int CustomCheckFailed()
        {
            return Interlocked.Decrement(ref totalFailures);
        }

        public int CustomCheckSucceeded()
        {
            return Interlocked.Increment(ref totalFailures);
        }

        int totalFailures;
    }
}