namespace ServiceControl.Audit.SagaAudit
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Auditing.MessagesView;
    using Raven.Client;
    using ServiceControl.SagaAudit;

    class GetSagaByIdApi : ApiBase<Guid, SagaHistory>
    {
        public GetSagaByIdApi(IDocumentStore documentStore) : base(documentStore)
        {
        }

        protected override async Task<QueryResult<SagaHistory>> Query(HttpRequestMessage request, Guid input)
        {
            using (var session = Store.OpenAsyncSession())
            {
                var sagaHistory = await
                    session.Query<SagaHistory, SagaDetailsIndex>()
                        .Statistics(out var stats)
                        .SingleOrDefaultAsync(x => x.SagaId == input)
                        .ConfigureAwait(false);

                if (sagaHistory == null)
                {
                    return QueryResult<SagaHistory>.Empty();
                }

                return new QueryResult<SagaHistory>(sagaHistory, new QueryStatsInfo(stats.IndexEtag, stats.TotalResults));
            }
        }
    }
}