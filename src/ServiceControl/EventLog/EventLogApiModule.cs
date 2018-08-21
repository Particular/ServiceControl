namespace ServiceControl.EventLog
{
    using System.Linq;
    using Infrastructure.Extensions;
    using Nancy;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class EventLogApiModule : BaseModule
    {
        public EventLogApiModule()
        {
            Get["/eventlogitems", true] = async (_, token) =>
            {
                using (var session = Store.OpenAsyncSession())
                {
                    var results = await session.Query<EventLogItem>().Statistics(out var stats).OrderByDescending(p => p.RaisedAt)
                        .Paging(Request)
                        .ToListAsync()
                        .ConfigureAwait(false);

                    return Negotiate.WithModel(results)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtag(stats);
                }
            };
        }
    }
}