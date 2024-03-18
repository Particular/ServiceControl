namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus.Logging;
    using Persistence.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Api;
    using ServiceControl.Api.Contracts;
    using ServiceControl.Operations.BodyStorage;
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
        IAuditCountApi auditCountApi,
        SearchApi api,
        SearchEndpointApi endpointApi)
        : ControllerBase
    {
        [Route("messages")]
        [HttpGet]
        public Task<IList<MessagesView>> Messages([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo,
            [FromQuery(Name = "include_system_messages")] bool includeSystemMessages) => allMessagesApi.Execute(
            new(pagingInfo, sortInfo, includeSystemMessages));

        [Route("endpoints/{endpoint}/messages")]
        [HttpGet]
        public Task<IList<MessagesView>> MessagesForEndpoint([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo,
            [FromQuery(Name = "include_system_messages")] bool includeSystemMessages, string endpoint) =>
            allMessagesForEndpointApi.Execute(new(pagingInfo, sortInfo, includeSystemMessages, endpoint));

        // the endpoint name is needed in the route to match the route and forward it as path and query to the remotes
        [Route("endpoints/{endpoint}/audit-count")]
        [HttpGet]
        public Task<IList<AuditCount>> GetEndpointAuditCounts([FromQuery] PagingInfo pagingInfo, string endpoint) =>
            auditCountApi.GetEndpointAuditCounts(pagingInfo.Page, pagingInfo.PageSize, endpoint);

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
                logger.Warn($"Failed to forward the request ot remote instance at {remote.BaseAddress + HttpContext.Request.GetEncodedPathAndQuery()}.", exception);
            }

            return Empty;
        }

        [Route("messages/search")]
        [HttpGet]
        public Task<IList<MessagesView>> Search([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string q) => api.Execute(new(pagingInfo, sortInfo, q));

        [Route("messages/search/{keyword}")]
        [HttpGet]
        public Task<IList<MessagesView>> SearchByKeyWord([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string keyword) =>
            api.Execute(new(pagingInfo, sortInfo, keyword?.Replace("/", @"\")));

        [Route("endpoints/{endpoint}/messages/search")]
        [HttpGet]
        public Task<IList<MessagesView>> Search([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string endpoint, string q) =>
            endpointApi.Execute(new(pagingInfo, sortInfo, endpoint, q));

        [Route("endpoints/{endpoint}/messages/search/{keyword}")]
        [HttpGet]
        public Task<IList<MessagesView>> SearchByKeyword([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string endpoint, string keyword) =>
            endpointApi.Execute(new(pagingInfo, sortInfo, endpoint, keyword));

        static ILog logger = LogManager.GetLogger(typeof(GetMessagesController));
    }
}