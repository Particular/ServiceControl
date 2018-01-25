namespace ServiceControl.CompositeViews.Endpoints
{
    using System.Linq;
    using System.Threading.Tasks;
    using Nancy;
    using Raven.Client;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.EndpointControl;

    public class GetKnownEndpointsApi : ScatterGatherApi<NoInput, KnownEndpointsView>
    {
        public override Task<QueryResult<KnownEndpointsView>> LocalQuery(Request request, NoInput input)
        {
            using (var session = Store.OpenSession())
            {
                RavenQueryStatistics stats;

                var results = session.Query<KnownEndpoint, KnownEndpointIndex>()
                    .Statistics(out stats)
                    .Select(x => new KnownEndpointsView
                    {
                        Id = x.Id,
                        HostDisplayName = x.HostDisplayName,
                        EndpointDetails = x.EndpointDetails
                    })
                    .ToList();

                return Task.FromResult(Results(results, stats));
            }
        }
    }
}