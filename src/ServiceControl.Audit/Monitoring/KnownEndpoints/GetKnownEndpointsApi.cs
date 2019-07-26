namespace ServiceControl.Audit.Monitoring
{
    using System.Linq;
    using System.Threading.Tasks;
    using Auditing.MessagesView;
    using Infrastructure;
    using Infrastructure.Extensions;
    using Nancy;
    using Raven.Client;

    class GetKnownEndpointsApi : ApiBase<NoInput, KnownEndpointsView[]>
    {
        public override async Task<QueryResult<KnownEndpointsView[]>> Query(Request request, NoInput input)
        {
            using (var session = Store.OpenAsyncSession())
            {
                var endpoints = await session.Query<EndpointDetails, EndpointsIndex>()
                    .Statistics(out var stats)
                    .ToListAsync()
                    .ConfigureAwait(false);

                var knownEndpoints = endpoints
                    .Select(x => new KnownEndpointsView
                    {
                        Id = DeterministicGuid.MakeId(x.Name, x.HostId.ToString()),
                        EndpointDetails = x,
                        HostDisplayName = x.Host
                    })
                    .ToArray();

                return new QueryResult<KnownEndpointsView[]>(knownEndpoints, stats.ToQueryStatsInfo());
            }
        }
    }
}