namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Persistence.Infrastructure;
    using ServiceControl.Persistence;

    [ApiController]
    [Route("api")]
    public class GetAllErrorsController(IErrorMessageDataStore store) : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorMessagesView)]
        [Route("errors")]
        [HttpGet]
        public async Task<IList<FailedMessageView>> ErrorsGet([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string status, string modified, string queueAddress)
        {
            var results = await store.ErrorGet(
                    status: status,
                    modified: modified,
                    queueAddress: queueAddress,
                    pagingInfo,
                    sortInfo
                    );

            Response.WithQueryStatsAndPagingInfo(results.QueryStats, pagingInfo);

            return results.Results;
        }

        [Authorize(Policy = Permissions.ErrorMessagesView)]
        [Route("errors")]
        [HttpHead]
        public async Task ErrorsHead(string status, string modified, string queueAddress)
        {
            var queryResult = await store.ErrorsHead(
                    status: status,
                    modified: modified,
                    queueAddress: queueAddress
                    );

            Response.WithQueryStatsInfo(queryResult);
        }

        [Authorize(Policy = Permissions.ErrorMessagesView)]
        [Route("endpoints/{endpointname}/errors")]
        [HttpGet]
        public async Task<IList<FailedMessageView>> ErrorsByEndpointName([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string status, string modified, string endpointName)
        {
            var results = await store.ErrorsByEndpointName(
                status: status,
                endpointName: endpointName,
                modified: modified,
                pagingInfo,
                sortInfo
                );

            Response.WithQueryStatsAndPagingInfo(results.QueryStats, pagingInfo);

            return results.Results;
        }

        [Authorize(Policy = Permissions.ErrorMessagesView)]
        [Route("errors/summary")]
        [HttpGet]
        public async Task<IDictionary<string, object>> ErrorsSummary() => await store.ErrorsSummary();
    }
}