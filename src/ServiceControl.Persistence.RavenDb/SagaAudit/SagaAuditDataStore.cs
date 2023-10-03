namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Threading.Tasks;
    using Raven.Client;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.SagaAudit;

    class SagaAuditDataStore : ISagaAuditDataStore
    {
        public SagaAuditDataStore(IDocumentStore store)
        {
            this.store = store;
        }

        public async Task<bool> StoreSnapshot(SagaSnapshot sagaSnapshot)
        {
            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(sagaSnapshot);
                await session.SaveChangesAsync();
                return true;
            }
        }

        public async Task<QueryResult<SagaHistory>> GetSagaById(Guid sagaId)
        {
            using (var session = store.OpenAsyncSession())
            {
                var sagaHistory = await
                    session.Query<SagaHistory, SagaDetailsIndex>()
                        .Statistics(out var stats)
                        .SingleOrDefaultAsync(x => x.SagaId == sagaId);

                if (sagaHistory == null)
                {
                    return QueryResult<SagaHistory>.Empty();
                }

                return new QueryResult<SagaHistory>(sagaHistory, new QueryStatsInfo(stats.IndexEtag, stats.TotalResults, stats.IsStale));
            }
        }

        readonly IDocumentStore store;
    }
}
