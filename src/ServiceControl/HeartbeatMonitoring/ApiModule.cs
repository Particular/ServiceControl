namespace ServiceControl.HeartbeatMonitoring
{
    using Operations.Heartbeats;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using Nancy;
    using System.Linq;

    public class ApiModule : BaseModule
    {
        public IDocumentStore Store { get; set; }

        public ApiModule()
        {
            Get["/heartbeats/stats"] = _ =>
            {
                using (var session = Store.OpenSession())
                {
                    var numberOfEndpointsDead = session.Query<Heartbeat>().Count(c => c.ReportedStatus == Status.Dead);
                    var numberOfEndpointsActive = session.Query<Heartbeat>().Count(c => c.ReportedStatus != Status.Dead);

                    return Negotiate.WithModel(new
                    {
                        ActiveEndpoints = numberOfEndpointsActive,
                        FailingEndpoints = numberOfEndpointsDead
                    });
                }
            };

            Get["/heartbeats"] = _ =>
            {
                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;
                    var results =
                        session.Query<Heartbeat>()
                            .Statistics(out stats)
                            .Paging(Request)
                            .ToArray();

                    return Negotiate
                        .WithModel(results)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };
        }
    }
}