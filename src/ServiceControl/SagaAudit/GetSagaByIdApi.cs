namespace ServiceControl.SagaAudit
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Nancy;
    using Raven.Client;
    using ServiceControl.CompositeViews.Messages;

    public class GetSagaByIdApi : ScatterGatherApi<Guid, SagaHistory>
    {
        public override async Task<QueryResult<SagaHistory>> LocalQuery(Request request, Guid sagaId)
        {
            using (var session = Store.OpenAsyncSession())
            {
                RavenQueryStatistics stats;
                var sagaHistory = await 
                    session.Query<SagaHistory, SagaDetailsIndex>()
                        .Statistics(out stats)
                        .SingleOrDefaultAsync(x => x.SagaId == sagaId)
                        .ConfigureAwait(false);

                if (sagaHistory == null)
                {
                    return QueryResult<SagaHistory>.Empty;
                }

                var lastModified = sagaHistory.Changes.OrderByDescending(x => x.FinishTime)
                    .Select(y => y.FinishTime)
                    .First();

                return new QueryResult<SagaHistory>(sagaHistory, new QueryStatsInfo(stats.IndexEtag, lastModified, stats.TotalResults));
            }
        }

        protected override SagaHistory ProcessResults(Request request, QueryResult<SagaHistory>[] results)
        {
            return results.Select(p => p.Results).First(x => x != null);
        }
    }
}