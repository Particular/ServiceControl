namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Mvc;
    using Persistence.Infrastructure;
    using ServiceControl.Persistence;

    [ApiController]
    [Route("api")]
    public class GetAllErrorsController(IErrorMessageDataStore store) : ControllerBase
    {
        [Route("errors")]
        [HttpGet]
        public async Task<IList<FailedMessageView>> ErrorsGet([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string status, string modified, string queueAddress)
        {
            var authInfo = AuthorizationInfo.FromClaims(HttpContext.User);

            var results = await store.ErrorGet(
                    status: status,
                    modified: modified,
                    queueAddress: queueAddress,
                    pagingInfo,
                    sortInfo,
                    authInfo
                    );

            Response.WithQueryStatsAndPagingInfo(results.QueryStats, pagingInfo);

            return results.Results;
        }

        [Route("errors")]
        [HttpHead]
        public async Task ErrorsHead(string status, string modified, string queueAddress)
        {
            var authInfo = AuthorizationInfo.FromClaims(HttpContext.User);

            var queryResult = await store.ErrorsHead(
                    status: status,
                    modified: modified,
                    queueAddress: queueAddress,
                    authInfo
                    );

            Response.WithQueryStatsInfo(queryResult);
        }

        [Route("endpoints/{endpointname}/errors")]
        [HttpGet]
        public async Task<IList<FailedMessageView>> ErrorsByEndpointName([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string status, string modified, string endpointName)
        {
            var authInfo = AuthorizationInfo.FromClaims(HttpContext.User);

            var results = await store.ErrorsByEndpointName(
                status: status,
                endpointName: endpointName,
                modified: modified,
                pagingInfo,
                sortInfo,
                authInfo
                );

            Response.WithQueryStatsAndPagingInfo(results.QueryStats, pagingInfo);

            return results.Results;
        }

        [Route("errors/summary")]
        [HttpGet]
        public async Task<IDictionary<string, object>> ErrorsSummary() => await store.ErrorsSummary();
    }
}