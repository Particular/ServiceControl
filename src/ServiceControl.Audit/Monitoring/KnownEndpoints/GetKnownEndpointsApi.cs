namespace ServiceControl.Audit.Monitoring
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Auditing.MessagesView;
    using Infrastructure;
    using Raven.Client.Documents;

    class GetKnownEndpointsApi : ApiBaseNoInput<IList<KnownEndpointsView>>
    {
        public GetKnownEndpointsApi(IDocumentStore documentStore) : base(documentStore)
        {
        }

        protected override async Task<QueryResult<IList<KnownEndpointsView>>> Query(HttpRequestMessage request)
        {
            using (var session = Store.OpenAsyncSession())
            {
                var endpoints = await session.Advanced.LoadStartingWithAsync<KnownEndpoint>(KnownEndpoint.CollectionName, pageSize: 1024)
                    .ConfigureAwait(false);

                var knownEndpoints = endpoints
                    .Select(x => new KnownEndpointsView
                    {
                        Id = DeterministicGuid.MakeId(x.Name, x.HostId.ToString()),
                        EndpointDetails = new EndpointDetails
                        {
                            Host = x.Host,
                            HostId = x.HostId,
                            Name = x.Name
                        },
                        HostDisplayName = x.Host
                    })
                    .ToList();

                return new QueryResult<IList<KnownEndpointsView>>(knownEndpoints, new QueryStatsInfo(string.Empty, knownEndpoints.Count));
            }
        }
    }
}