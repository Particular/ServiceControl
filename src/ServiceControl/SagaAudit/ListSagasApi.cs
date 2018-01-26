namespace ServiceControl.SagaAudit
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Nancy;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Infrastructure.Extensions;

    public class ListSagasApi : ScatterGatherApi<NoInput, List<SagaListIndex.Result>>
    {
        public override async Task<QueryResult<List<SagaListIndex.Result>>> LocalQuery(Request request, NoInput input, string instanceId)
        {
            using (var session = Store.OpenAsyncSession())
            {
                RavenQueryStatistics stats;
                var results = await session.Query<SagaListIndex.Result, SagaListIndex>()
                    .Statistics(out stats)
                    .Paging(request)
                    .ToListAsync()
                    .ConfigureAwait(false);

                results.ForEach(r => r.Uri = $"{r.Uri}?instance_id={instanceId}");

                return Results(results.ToList(), stats);
            }
        }

        protected override List<SagaListIndex.Result> ProcessResults(Request request, QueryResult<List<SagaListIndex.Result>>[] results)
        {
            return results.SelectMany(p => p.Results).ToList();
        }
    }
}