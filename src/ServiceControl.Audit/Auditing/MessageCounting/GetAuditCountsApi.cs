namespace ServiceControl.Audit.Auditing.MessageCounting
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Auditing.MessagesView;
    using ServiceControl.Audit.Infrastructure.Settings;
    using ServiceControl.Audit.Persistence;

    class GetAuditCountsApi : ApiBase<NoInput, DailyAuditCountResult>
    {
        readonly Settings settings;

        public GetAuditCountsApi(IAuditDataStore dataStore, Settings settings) : base(dataStore)
        {
            this.settings = settings;
        }

        protected override async Task<QueryResult<DailyAuditCountResult>> Query(HttpRequestMessage request, NoInput input)
        {
            var data = await DataStore.QueryAuditCounts().ConfigureAwait(false);

            var result = new DailyAuditCountResult
            {
                AuditRetention = settings.AuditRetentionPeriod,
                Days = data.Results
            };

            return new QueryResult<DailyAuditCountResult>(result, data.QueryStats);
        }
    }
}
