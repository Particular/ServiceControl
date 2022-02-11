namespace ServiceControl.Audit.SagaAudit
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Auditing.MessagesView;
    using Infrastructure.SQL;
    using ServiceControl.SagaAudit;

    class GetSagaByIdApi : ApiBase<Guid, SagaHistory>
    {
        public GetSagaByIdApi(SqlQueryStore queryStore) : base(queryStore)
        {
        }

        protected override async Task<QueryResult<SagaHistory>> Query(HttpRequestMessage request, Guid input)
        {
            var result = await Store.GetSagaById(input, out var etag, out var totalResults).ConfigureAwait(false);

            return new QueryResult<SagaHistory>(result, new QueryStatsInfo(etag, totalResults));
        }
    }
}