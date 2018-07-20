namespace ServiceControl.SagaAudit
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Nancy;
    using Raven.Client;
    using CompositeViews.Messages;

    public class GetSagaByIdApi : ScatterGatherApi<Guid, SagaHistory>
    {
        public override async Task<QueryResult<SagaHistory>> LocalQuery(Request request, Guid sagaId)
        {
            using (var session = Store.OpenAsyncSession())
            {
                var sagaHistory = await 
                    session.Query<SagaHistory, SagaDetailsIndex>()
                        .Statistics(out var stats)
                        .SingleOrDefaultAsync(x => x.SagaId == sagaId)
                        .ConfigureAwait(false);

                if (sagaHistory == null)
                {
                    return QueryResult<SagaHistory>.Empty();
                }

                return new QueryResult<SagaHistory>(sagaHistory, new QueryStatsInfo(stats.IndexEtag, stats.TotalResults));
            }
        }

        protected override SagaHistory ProcessResults(Request request, QueryResult<SagaHistory>[] results)
        {
            return results.Select(p => p.Results).SingleOrDefault(x => x != null);
        }
    }
}