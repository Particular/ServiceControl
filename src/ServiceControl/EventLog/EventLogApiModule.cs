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
            Get["/eventlogitems", true] = async (_, token) =>
            {
                using (var session = Store.OpenAsyncSession())
                {
                    RavenQueryStatistics stats;
                    var results = await session.Query<EventLogItem>().Statistics(out stats).OrderByDescending(p => p.RaisedAt)
                    .Paging(Request)
                    .ToListAsync()
                    .ConfigureAwait(false);

                    return Negotiate.WithModel(results)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };
        }
    }
}
