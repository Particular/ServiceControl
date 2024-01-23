namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Primitives;
    using NServiceBus.Logging;
    using Persistence.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.MessageCounting;
    using ServiceControl.Operations.BodyStorage;
    using Yarp.ReverseProxy.Forwarder;
    using static Infrastructure.WebApi.RemoteInstanceServiceCollectionExtensions;

    // All routes matching `messages/*` must be in this controller as WebAPI cannot figure out the overlapping routes
    // from `messages/{*catchAll}` if they're in separate controllers.
    [ApiController]
    [Route("api")]
    public class GetMessagesController(
        IBodyStorage bodyStorage,
        Settings settings,
        IHttpClientFactory httpClientFactory,
        IHttpForwarder forwarder,
        GetAllMessagesApi allMessagesApi,
        GetAllMessagesForEndpointApi allMessagesForEndpointApi,
        GetAuditCountsForEndpointApi auditCountsForEndpointApi,
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
            auditCountsForEndpointApi.Execute(new(pagingInfo, endpoint));

        [Route("messages/{id}/body")]
        [HttpGet]
        public async Task<ActionResult<Stream>> Get(string id, [FromQuery(Name = "instance_id")] string instanceId)
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
                Response.Headers.ContentType = result.ContentType ?? "text/*";
                Response.Headers.ContentLength = result.BodySize;

                return result.Stream;
            }

            var remote = settings.RemoteInstances.SingleOrDefault(r => r.InstanceId == instanceId);

            if (remote == null)
            {
                return BadRequest();
            }

            var forwarderError = await forwarder.SendAsync(HttpContext, remote.ApiUri, httpClientFactory.CreateClient(RemoteForwardingHttpClientName));
            if (forwarderError != ForwarderError.None && HttpContext.GetForwarderErrorFeature()?.Exception is { } exception)
            {
                logger.Warn($"Failed to forward the request ot remote instance at {remote.ApiUri + HttpContext.Request.GetEncodedPathAndQuery()}.", exception);
            }

            return Empty;
        }

        // TODO Is this still needed?
        // Possible a message may contain a slash or backslash, either way http.sys will rewrite it to forward slash,
        // and then the "normal" route above will not activate, resulting in 404 if this route is not present.
        [Route("messages/{*catchAll}")]
        [HttpGet]
        public async Task<ActionResult<Stream>> CatchAll(string catchAll, [FromQuery(Name = "instance_id")] string instanceId)
        {
            if (!string.IsNullOrEmpty(catchAll) && catchAll.EndsWith("/body"))
            {
                var id = catchAll[..^5];
                return await Get(id, instanceId);
            }

            return NotFound();
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