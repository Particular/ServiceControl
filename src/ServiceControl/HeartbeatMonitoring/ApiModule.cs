namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using Infrastructure.Extensions;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using Nancy;
    using System.Linq;

    public class ApiModule : BaseModule
    {
        public HeartbeatsComputation HeartbeatsComputation { get; set; }

        public ApiModule()
        {
            Get["/heartbeats/stats"] = _ =>
            {
                var heartbeatsStats = HeartbeatsComputation.Current;

                return Negotiate.WithModel(new
                {
                    heartbeatsStats.Active,
                    Failing = heartbeatsStats.Dead
                });
            };

            Delete["/heartbeats/{id}"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    Guid id = parameters.id;

                    var heartbeat = session.Load<Heartbeat>(id);

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