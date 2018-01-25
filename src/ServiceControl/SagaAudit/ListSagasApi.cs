namespace ServiceControl.SagaAudit
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Nancy;
    using Raven.Client;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Infrastructure.Extensions;

    public class ListSagasApi : ScatterGatherApi<NoInput, List<SagaListIndex.Result>>
    {
        public override async Task<QueryResult<List<SagaListIndex.Result>>> LocalQuery(Request request, NoInput input)
        {
            using (var session = Store.OpenAsyncSession())
            {
                RavenQueryStatistics stats;
                var results = await session.Query<SagaListIndex.Result, SagaListIndex>()
                    .Statistics(out stats)
                    .Paging(request)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return Results(results.ToList(), stats);
            }
        }

        protected override List<SagaListIndex.Result> ProcessResults(Request request, QueryResult<List<SagaListIndex.Result>>[] results)
        {
            return results.SelectMany(p => p.Results).ToList();
        }
    }
}