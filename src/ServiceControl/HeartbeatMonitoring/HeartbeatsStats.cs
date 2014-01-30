namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using System.Linq;
    using Raven.Client;

    public static class HeartbeatsStats
    {
        public static Tuple<int, int> Retrieve(IDocumentStore store)
        {
            var numberOfEndpointsDead = 0;
            var numberOfEndpointsActive = 0;

            using (var session = store.OpenSession())
            {
                RavenQueryStatistics stats1, stats2;

                session.Query<Heartbeat>().Statistics(out stats1).Where(c => c.ReportedStatus == Status.Dead).Take(0).Lazily(heartbeats => numberOfEndpointsDead = stats1.TotalResults);
                session.Query<Heartbeat>().Statistics(out stats2).Where(c => c.ReportedStatus != Status.Dead).Take(0).Lazily(heartbeats => numberOfEndpointsActive = stats2.TotalResults);

                session.Advanced.Eagerly.ExecuteAllPendingLazyOperations();
            }

            return new Tuple<int, int>(numberOfEndpointsDead, numberOfEndpointsActive);
        }
    }
}