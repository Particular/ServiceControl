namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Net;
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
        public async Task<HttpResponseMessage> ErrorsGet(
            [FromUri] string status,
            [FromUri] string modified,
            [FromUri] string queueAddress)
        {
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

            return Negotiator
                .FromModel(Request, results.Results)
                .WithPagingLinksAndTotalCount(results.QueryStats.TotalCount, Request)
                .WithEtag(results.QueryStats.ETag);
        }

        [Route("errors")]
        [HttpHead]
        public async Task<HttpResponseMessage> ErrorsHead(
            [FromUri] string status,
            [FromUri] string modified,
            [FromUri] string queueAddress
            )
        {
            var queryResult = await dataStore.ErrorsHead(
                    status: status,
                    modified: modified,
                    queueAddress: queueAddress
                    )
                .ConfigureAwait(false);

            var response = Request.CreateResponse(HttpStatusCode.OK);

            return response
                .WithTotalCount(queryResult.TotalCount)
                .WithEtag(queryResult.ETag);
        }

        [Route("endpoints/{endpointname}/errors")]
        [HttpGet]
        public async Task<HttpResponseMessage> ErrorsByEndpointName(
            [FromUri] string endpointName,
            [FromUri] string status,
            [FromUri] string modified
            )
        {
            var sortInfo = Request.GetSortInfo();
            var pagingInfo = Request.GetPagingInfo();

            var results = await dataStore.ErrorsByEndpointName(
                status: status,
                endpointName: endpointName,
                modified: modified,
                pagingInfo,
                sortInfo
                ).ConfigureAwait(false);

            return Negotiator
                .FromModel(Request, results)
                .WithPagingLinksAndTotalCount(results.QueryStats.TotalCount, Request)
                .WithEtag(results.QueryStats.ETag);
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