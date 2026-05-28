namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Api.Contracts;
    using Infrastructure.WebApi;
    using MessageCounting;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Operations.BodyStorage;
    using Persistence.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.Auth.Rbac;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControl.Infrastructure.WebApi.Auth;
    using Yarp.ReverseProxy.Forwarder;

    // All routes matching `messages/*` must be in this controller as WebAPI cannot figure out the overlapping routes
    // from `messages/{*catchAll}` if they're in separate controllers.
    [ApiController]
    [Route("api")]
    public class GetMessagesController(
        IBodyStorage bodyStorage,
        Settings settings,
        HttpMessageInvoker httpMessageInvoker,
        IHttpForwarder forwarder,
        GetAllMessagesApi allMessagesApi,
        GetAllMessagesForEndpointApi allMessagesForEndpointApi,
        GetAuditCountsForEndpointApi auditCountsForEndpointApi,
        SearchApi api,
        SearchEndpointApi endpointApi,
        ILogger<GetMessagesController> logger)
        : ControllerBase
    {
        [RequirePermission(Permissions.MessagesView)]
        [Authorize(Policy = Permissions.MessagesView)]
        [Route("messages")]
        [HttpGet]
        public async Task<IList<MessagesView>> Messages([FromQuery] PagingInfo pagingInfo,
            [FromQuery] SortInfo sortInfo,
            [FromQuery(Name = "include_system_messages")]
            bool includeSystemMessages)
        {
            QueryResult<IList<MessagesView>> result = await allMessagesApi.Execute(
                new ScatterGatherApiMessageViewWithSystemMessagesContext(pagingInfo, sortInfo, includeSystemMessages),
                Request.GetEncodedPathAndQuery());

            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);
            return result.Results;
        }

        [RequirePermission(Permissions.MessagesView)]
        [Authorize(Policy = Permissions.MessagesView)]
        [Route("endpoints/{endpoint}/messages")]
        [HttpGet]
        public async Task<IList<MessagesView>> MessagesForEndpoint([FromQuery] PagingInfo pagingInfo,
            [FromQuery] SortInfo sortInfo,
            [FromQuery(Name = "include_system_messages")]
            bool includeSystemMessages, string endpoint)
        {
            QueryResult<IList<MessagesView>> result = await allMessagesForEndpointApi.Execute(
                new AllMessagesForEndpointContext(pagingInfo, sortInfo, includeSystemMessages, endpoint),
                Request.GetEncodedPathAndQuery());

            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);
            return result.Results;
        }

        // the endpoint name is needed in the route to match the route and forward it as path and query to the remotes
        [RequirePermission(Permissions.MessagesView)]
        [Authorize(Policy = Permissions.MessagesView)]
        [Route("endpoints/{endpoint}/audit-count")]
        [HttpGet]
        public async Task<IList<AuditCount>> GetEndpointAuditCounts([FromQuery] PagingInfo pagingInfo, string endpoint)
        {
            QueryResult<IList<AuditCount>> result = await auditCountsForEndpointApi.Execute(
                new AuditCountsForEndpointContext(pagingInfo, endpoint), Request.GetEncodedPathAndQuery());

            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);
            return result.Results;
        }

        [RequirePermission(Permissions.MessagesView)]
        [Authorize(Policy = Permissions.MessagesView)]
        [Route("messages/{id}/body")]
        [HttpGet]
        public async Task<IActionResult> Get(string id, [FromQuery(Name = "instance_id")] string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || instanceId == settings.InstanceId)
            {
                var result = await bodyStorage.TryFetch(id);

                if (result == null)
                {
                    return NotFound();
                }

                if (!result.HasResult)
                {
                    return NoContent();
                }

                Response.Headers.ETag = result.Etag;
                return File(result.Stream, result.ContentType ?? "text/*");
            }

            var remote = settings.RemoteInstances.SingleOrDefault(r => r.InstanceId == instanceId);

            if (remote == null)
            {
                return BadRequest();
            }

            var forwarderError = await forwarder.SendAsync(HttpContext, remote.BaseAddress, httpMessageInvoker);
            if (forwarderError != ForwarderError.None && HttpContext.GetForwarderErrorFeature()?.Exception is { } exception)
            {
                logger.LogWarning(exception, "Failed to forward the request to remote instance at {RemoteInstanceUrl}",
                    remote.BaseAddress + HttpContext.Request.GetEncodedPathAndQuery());
            }

            return Empty;
        }

        [RequirePermission(Permissions.MessagesView)]
        [Authorize(Policy = Permissions.MessagesView)]
        [Route("messages/search")]
        [HttpGet]
        public async Task<IList<MessagesView>> Search([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo,
            string q)
        {
            QueryResult<IList<MessagesView>> result = await api.Execute(new SearchApiContext(pagingInfo, sortInfo, q),
                Request.GetEncodedPathAndQuery());

            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);
            return result.Results;
        }

        [RequirePermission(Permissions.MessagesView)]
        [Authorize(Policy = Permissions.MessagesView)]
        [Route("messages/search/{keyword}")]
        [HttpGet]
        public async Task<IList<MessagesView>> SearchByKeyWord([FromQuery] PagingInfo pagingInfo,
            [FromQuery] SortInfo sortInfo, string keyword)
        {
            QueryResult<IList<MessagesView>> result = await api.Execute(
                new SearchApiContext(pagingInfo, sortInfo, keyword?.Replace("/", @"\")),
                Request.GetEncodedPathAndQuery());

            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);
            return result.Results;
        }

        [RequirePermission(Permissions.MessagesView)]
        [Authorize(Policy = Permissions.MessagesView)]
        [Route("endpoints/{endpoint}/messages/search")]
        [HttpGet]
        public async Task<IList<MessagesView>> Search([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo,
            string endpoint, string q)
        {
            QueryResult<IList<MessagesView>> result = await endpointApi.Execute(
                new SearchEndpointContext(pagingInfo, sortInfo, endpoint, q), Request.GetEncodedPathAndQuery());

            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);
            return result.Results;
        }

        [RequirePermission(Permissions.MessagesView)]
        [Authorize(Policy = Permissions.MessagesView)]
        [Route("endpoints/{endpoint}/messages/search/{keyword}")]
        [HttpGet]
        public async Task<IList<MessagesView>> SearchByKeyword([FromQuery] PagingInfo pagingInfo,
            [FromQuery] SortInfo sortInfo, string endpoint, string keyword)
        {
            QueryResult<IList<MessagesView>> result = await endpointApi.Execute(
                new SearchEndpointContext(pagingInfo, sortInfo, endpoint, keyword), Request.GetEncodedPathAndQuery());

            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);
            return result.Results;
        }
    }
}