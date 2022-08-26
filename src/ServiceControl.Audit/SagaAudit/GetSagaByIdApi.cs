namespace ServiceControl.Audit.SagaAudit
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Auditing.MessagesView;
    using ServiceControl.Audit.Persistence;
    using ServiceControl.SagaAudit;

    class GetSagaByIdApi : ApiBase<Guid, SagaHistory>
    {
        public GetSagaByIdApi(IAuditDataStore dataStore) : base(dataStore)
        {
        }

        protected override async Task<QueryResult<SagaHistory>> Query(HttpRequestMessage request, Guid input)
        {
            return await DataStore.QuerySagaHistoryById(input).ConfigureAwait(false);
        }
    }
}