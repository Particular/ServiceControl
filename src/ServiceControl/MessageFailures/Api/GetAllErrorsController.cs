namespace ServiceControl.MessageFailures.Api
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Infrastructure.WebApi;
    using Persistence.Infrastructure;
    using ServiceControl.Persistence;

    class GetAllErrorsController : ApiController
    {
        public GetAllErrorsController(IErrorMessageDataStore dataStore)
        {
            this.dataStore = dataStore;
        }

        [Route("errors")]
        [HttpGet]
        public async Task<HttpResponseMessage> ErrorsGet()
        {
            string status = Request.GetStatus();
            string modified = Request.GetModified();
            string queueAddress = Request.GetQueueAddress();

            var sortInfo = Request.GetSortInfo();
            var pagingInfo = Request.GetPagingInfo();

            var results = await dataStore.ErrorGet(
                    status: status,
                    modified: modified,
                    queueAddress: queueAddress,
                    pagingInfo,
                    sortInfo
                    )
                .ConfigureAwait(false);

            return Negotiator.FromQueryResult(Request, results);
        }

        [Route("errors")]
        [HttpHead]
        public async Task<HttpResponseMessage> ErrorsHead()
        {
            string status = Request.GetStatus();
            string modified = Request.GetModified();
            string queueAddress = Request.GetQueueAddress();

            var queryResult = await dataStore.ErrorsHead(
                    status: status,
                    modified: modified,
                    queueAddress: queueAddress
                    )
                .ConfigureAwait(false);


            return Negotiator.FromQueryStatsInfo(Request, queryResult);
        }

        [Route("endpoints/{endpointname}/errors")]
        [HttpGet]
        public async Task<HttpResponseMessage> ErrorsByEndpointName()
        {
            string status = Request.GetStatus();
            string modified = Request.GetModified();
            string endpointName = Request.GetEndpointName();

            var sortInfo = Request.GetSortInfo();
            var pagingInfo = Request.GetPagingInfo();

            var results = await dataStore.ErrorsByEndpointName(
                status: status,
                endpointName: endpointName,
                modified: modified,
                pagingInfo,
                sortInfo
                ).ConfigureAwait(false);

            return Negotiator.FromQueryResult(Request, results);
        }

        [Route("errors/summary")]
        [HttpGet]
        public async Task<HttpResponseMessage> ErrorsSummary()
        {
            var results = await dataStore.ErrorsSummary()
                .ConfigureAwait(false);

            return Negotiator.FromModel(Request, results);
        }

        readonly IErrorMessageDataStore dataStore;
    }
}