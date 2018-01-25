namespace ServiceControl.SagaAudit
{
    using System.Linq;
    using System.Threading.Tasks;
    using Nancy;
    using Raven.Client;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Infrastructure.Extensions;

    public class ListSagasApi : ScatterGatherApi<NoInput, SagaListIndex.Result[]>
    {
        public override async Task<QueryResult<SagaListIndex.Result[]>> LocalQuery(Request request, NoInput input)
        {
            using (var session = Store.OpenAsyncSession())
            {
                RavenQueryStatistics stats;
                var results = await session.Query<SagaListIndex.Result, SagaListIndex>()
                    .Statistics(out stats)
                    .Paging(request)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return Results(results.ToArray(), stats);
            }
        }

        protected override SagaListIndex.Result[] ProcessResults(Request request, QueryResult<SagaListIndex.Result[]>[] results)
        {
            return results.SelectMany(p => p.Results).ToArray();
        }
    }
}