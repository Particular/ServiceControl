namespace ServiceControl.EventLog
{
    using System.Linq;
    using Nancy;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.Infrastructure.Extensions;

    public class EventLogApiModule : BaseModule
    {
        public EventLogApiModule()
        {
            Get["/eventlogitems"] = _ =>
            {
                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;
                    var results = session.Query<EventLogItem>().Statistics(out stats).OrderByDescending(p => p.RaisedAt)
                    .Paging(Request)
                    .ToArray();

                    return Negotiate.WithModel(results)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };
        }
    }
}
