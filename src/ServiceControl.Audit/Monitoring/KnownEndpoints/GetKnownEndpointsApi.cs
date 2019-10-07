namespace ServiceControl.Audit.Monitoring
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Auditing.MessagesView;
    using Infrastructure;
    using Infrastructure.Extensions;
    using Raven.Client;

    class GetKnownEndpointsApi : ApiBaseNoInput<IList<KnownEndpointsView>>
    {
        public GetKnownEndpointsApi(IDocumentStore documentStore) : base(documentStore)
        {
        }

        protected override async Task<QueryResult<IList<KnownEndpointsView>>> Query(HttpRequestMessage request)
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
                    .ToList();

                return new QueryResult<IList<KnownEndpointsView>>(knownEndpoints, stats.ToQueryStatsInfo());
            }
        }
    }
}