namespace ServiceControl.HeartbeatMonitoring
{
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using Nancy;
    using System.Linq;

    public class ApiModule : BaseModule
    {
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

            Delete["/heartbeats/{id}"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    string id = parameters.id;

                    var heartbeat = session.Load<Heartbeat>(string.Format("heartbeats/{0}", id));

                    if (heartbeat != null)
                    {
                        session.Delete(heartbeat);
                        session.SaveChanges();
                    }
                }

                return HttpStatusCode.NoContent;
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