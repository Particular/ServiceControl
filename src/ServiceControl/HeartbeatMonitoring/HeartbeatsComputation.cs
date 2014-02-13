namespace ServiceControl.HeartbeatMonitoring
{
    using System.Linq;
    using CompositeViews.Endpoints;
    using EndpointControl;
    using NServiceBus;
    using Raven.Client;

    public class HeartbeatsComputation : INeedInitialization
    {
        public HeartbeatsComputation()
        {
            // Need this because INeedInitialization does not use DI instead use Activator.CreateInstance
        }

        public HeartbeatsComputation(IDocumentStore store)
        {
            this.store = store;
            Initialise();
        }

        public HeartbeatsStats Current
        {
            get
            {
                lock (locker)
                {
                    return new HeartbeatsStats(numberOfEndpointsActive, numberOfEndpointsDead);
                }
            }
        }

        public void Init()
        {
            Configure.Component<HeartbeatsComputation>(DependencyLifecycle.SingleInstance);
        }

        public HeartbeatsStats NewHeartbeatingEndpointDetected()
        {
            lock (locker)
            {
                return new HeartbeatsStats(++numberOfEndpointsActive, numberOfEndpointsDead);
            }
        }

        public HeartbeatsStats EndpointFailedToHeartbeat()
        {
            lock (locker)
            {
                return new HeartbeatsStats(--numberOfEndpointsActive, ++numberOfEndpointsDead);
            }
        }

        public HeartbeatsStats EndpointHeartbeatRestored()
        {
            lock (locker)
            {
                return new HeartbeatsStats(++numberOfEndpointsActive, --numberOfEndpointsDead);
            }
        }

        public HeartbeatsStats Reset()
        {
            lock (locker)
            {
                Initialise();
                return new HeartbeatsStats(numberOfEndpointsActive, numberOfEndpointsDead);
            }
        }

        void Initialise()
        {
            using (var session = store.OpenSession())
            {
                var total = 0;
                RavenQueryStatistics stats1, stats2;

                // Workaround to do Lazily Count, see https://groups.google.com/d/msg/ravendb/ptgTQbrPfzI/w9QJ0wdYkc4J
                // Raven v3 should support this natively, see http://issues.hibernatingrhinos.com/issue/RavenDB-1310

                session.Query<KnownEndpoint, KnownEndpointIndex>()
                    .Customize(c => c.WaitForNonStaleResultsAsOfLastWrite())
                    .Statistics(out stats1)
                    .Where(endpoint => endpoint.MonitorHeartbeat)
                    .Take(0)
                    .Lazily(heartbeats => total = stats1.TotalResults);
                session.Query<Heartbeat>()
                    .Customize(c => c.WaitForNonStaleResultsAsOfLastWrite())
                    .Statistics(out stats2)
                    .Where(heartbeat => heartbeat.ReportedStatus == Status.Beating)
                    .Take(0)
                    .Lazily(heartbeats => numberOfEndpointsActive = stats2.TotalResults);

                session.Advanced.Eagerly.ExecuteAllPendingLazyOperations();

                numberOfEndpointsDead = total - numberOfEndpointsActive;
            }
        }

        readonly object locker = new object();
        readonly IDocumentStore store;
        int numberOfEndpointsActive;
        int numberOfEndpointsDead;

        public class HeartbeatsStats
        {
            public HeartbeatsStats(int active, int dead)
            {
                Active = active;
                Dead = dead;
            }

            public readonly int Active;
            public readonly int Dead;
        }
    }
}