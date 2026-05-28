namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Persistence.Infrastructure;
    using ServiceControl.Infrastructure.Auth.Rbac;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControl.Infrastructure.WebApi.Auth;
    using ServiceControl.Persistence;

    [ApiController]
    [Route("api")]
    public class GetAllErrorsController(
        IErrorMessageDataStore store,
        IPermissionEvaluator permissionEvaluator) : ControllerBase
    {
        [RequirePermission(Permissions.MessagesView)]
        [Authorize(Policy = Permissions.MessagesView)]
        [Route("errors")]
        [HttpGet]
        public async Task<IList<FailedMessageView>> ErrorsGet([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string status, string modified, string queueAddress)
        {
            // R1: resolve the caller's permitted queue scope and push it into the query before
            // paging, so that Total-Count and page sizes reflect only messages in scope.
            // Null means unrestricted (admin / no scoped grants).
            var queueScope = permissionEvaluator.ResolveQueueScope(User, Permissions.MessagesView);

            var results = await store.ErrorGet(
                    status: status,
                    modified: modified,
                    queueAddress: queueAddress,
                    pagingInfo,
                    sortInfo,
                    queueScope
                    );

            Response.WithQueryStatsAndPagingInfo(results.QueryStats, pagingInfo);

            return results.Results;
        }

        [RequirePermission(Permissions.MessagesView)]
        [Authorize(Policy = Permissions.MessagesView)]
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

        [RequirePermission(Permissions.MessagesView)]
        [Authorize(Policy = Permissions.MessagesView)]
        [Route("endpoints/{endpointname}/errors")]
        [HttpGet]
        public async Task<IList<FailedMessageView>> ErrorsByEndpointName([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string status, string modified, string endpointName)
        {
            // R1: resolve the caller's permitted queue scope and push it into the query before
            // paging, so that Total-Count and page sizes reflect only messages in scope.
            var queueScope = permissionEvaluator.ResolveQueueScope(User, Permissions.MessagesView);

            var results = await store.ErrorsByEndpointName(
                status: status,
                endpointName: endpointName,
                modified: modified,
                pagingInfo,
                sortInfo,
                queueScope
                );

            Response.WithQueryStatsAndPagingInfo(results.QueryStats, pagingInfo);

            return results.Results;
        }

        [RequirePermission(Permissions.MessagesView)]
        [Authorize(Policy = Permissions.MessagesView)]
        [Route("errors/summary")]
        [HttpGet]
        public async Task<IDictionary<string, object>> ErrorsSummary() => await store.ErrorsSummary();
    }
}
