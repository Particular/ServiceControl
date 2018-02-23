namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using Nancy;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.Infrastructure.Extensions;

    public class QueueAddressModule : BaseModule
    {
        public QueueAddressModule()
        {
            Get["/errors/queues/addresses", true] = async (parameters, token) =>
            {
                using (var session = Store.OpenAsyncSession())
                {
                    RavenQueryStatistics stats;

                    var addresses = await session
                        .Query<QueueAddress, QueueAddressIndex>()
                        .Statistics(out stats)
                        .Paging(Request)
                        .ToListAsync()
                        .ConfigureAwait(false);

                    return Negotiate
                        .WithModel(addresses)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };

            Get["/errors/queues/addresses/search/{search}", true] = async (parameters, token) =>
            {
                using (var session = Store.OpenAsyncSession())
                {
                    if (!parameters.search.HasValue)
                    {
                        return HttpStatusCode.BadRequest;
                    }

                    string search = parameters.search;

                    if (string.IsNullOrWhiteSpace(search))
                    {
                        return HttpStatusCode.BadRequest;
                    }

                    RavenQueryStatistics stats;

                    var failedMessageQueues =
                        await session.Query<QueueAddress, QueueAddressIndex>()
                        .Statistics(out stats)
                        .Where(q => q.PhysicalAddress.StartsWith(search))
                        .OrderBy(q => q.PhysicalAddress)
                        .Paging(Request)
                        .ToListAsync()
                        .ConfigureAwait(false);

                    return Negotiate
                        .WithModel(failedMessageQueues)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };
        }
    }
}
