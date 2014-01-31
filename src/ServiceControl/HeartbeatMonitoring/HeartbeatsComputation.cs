namespace ServiceControl.HeartbeatMonitoring
{
    using System.Linq;
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

        void Initialise()
        {
            using (var session = store.OpenSession())
            {
                RavenQueryStatistics stats1, stats2;

                // Workaround to do Lazily Count, see https://groups.google.com/d/msg/ravendb/ptgTQbrPfzI/w9QJ0wdYkc4J
                // Raven v3 should support this natively, see http://issues.hibernatingrhinos.com/issue/RavenDB-1310

                session.Query<Heartbeat>()
                    .Statistics(out stats1)
                    .Where(c => c.ReportedStatus == Status.Dead)
                    .Take(0)
                    .Lazily(heartbeats => numberOfEndpointsDead = stats1.TotalResults);
                session.Query<Heartbeat>()
                    .Statistics(out stats2)
                    .Where(c => c.ReportedStatus == Status.Beating)
                    .Take(0)
                    .Lazily(heartbeats => numberOfEndpointsActive = stats2.TotalResults);

                session.Advanced.Eagerly.ExecuteAllPendingLazyOperations();
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