namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Persistence;

    [ApiController]
    [Route("api")]
    public class GetMessagesController(IAuditDataStore dataStore, ILogger<GetMessagesController> logger) : ControllerBase
    {
        [Route("messages")]
        [HttpGet]
        public async Task<IList<MessagesView>> GetAllMessages([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, [FromQuery(Name = "include_system_messages")] bool includeSystemMessages, CancellationToken cancellationToken)
        {
            var hasAuthHeader = HttpContext.Request.Headers.ContainsKey("Authorization");
            logger.LogDebug("Received request to /api/messages. Has Authorization header: {HasAuthHeader}", hasAuthHeader);

            var result = await dataStore.GetMessages(includeSystemMessages, pagingInfo, sortInfo, cancellationToken: cancellationToken);
            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);
            return result.Results;
        }

        [Route("endpoints/{endpoint}/messages")]
        [HttpGet]
        public async Task<IList<MessagesView>> GetEndpointMessages([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, [FromQuery(Name = "include_system_messages")] bool includeSystemMessages, string endpoint, CancellationToken cancellationToken)
        {
            var hasAuthHeader = HttpContext.Request.Headers.ContainsKey("Authorization");
            logger.LogDebug("Received request to /api/endpoints/{Endpoint}/messages. Has Authorization header: {HasAuthHeader}", endpoint, hasAuthHeader);

            var result = await dataStore.QueryMessagesByReceivingEndpoint(includeSystemMessages, endpoint, pagingInfo, sortInfo, cancellationToken: cancellationToken);
            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);
            return result.Results;
        }

        [Route("endpoints/{endpoint}/audit-count")]
        [HttpGet]
        public async Task<IList<AuditCount>> GetEndpointAuditCounts([FromQuery] PagingInfo pagingInfo, string endpoint, CancellationToken cancellationToken)
        {
            var hasAuthHeader = HttpContext.Request.Headers.ContainsKey("Authorization");
            logger.LogDebug("Received request to /api/endpoints/{Endpoint}/audit-count. Has Authorization header: {HasAuthHeader}", endpoint, hasAuthHeader);

            var result = await dataStore.QueryAuditCounts(endpoint, cancellationToken);
            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);
            return result.Results;
        }

        [Route("messages/{id}/body")]
        [HttpGet]
        public async Task<IActionResult> Get(string id, CancellationToken cancellationToken)
        {
            var hasAuthHeader = HttpContext.Request.Headers.ContainsKey("Authorization");
            logger.LogDebug("Received request to /api/messages/{Id}/body. Has Authorization header: {HasAuthHeader}", id, hasAuthHeader);

            var result = await dataStore.GetMessageBody(id, cancellationToken);

            if (result.Found == false)
            {
                return NotFound();
            }

            if (result.HasContent == false)
            {
                return NoContent();
            }

            if (result.StringContent == null && result.StreamContent == null)
            {
                throw new Exception($"Metadata for message '{id}' indicated that a body was present but no content could be found in storage");
            }

            Response.Headers.ETag = result.ETag;
            var contentType = result.ContentType ?? "text/*";
            return result.StringContent != null ? Content(result.StringContent, contentType) : File(result.StreamContent, contentType);
        }

        [Route("messages/search")]
        [HttpGet]
        public async Task<IList<MessagesView>> Search([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string q, CancellationToken cancellationToken)
        {
            var hasAuthHeader = HttpContext.Request.Headers.ContainsKey("Authorization");
            logger.LogDebug("Received request to /api/messages/search. Has Authorization header: {HasAuthHeader}", hasAuthHeader);

            var result = await dataStore.QueryMessages(q, pagingInfo, sortInfo, cancellationToken: cancellationToken);
            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);
            return result.Results;
        }

        [Route("messages/search/{keyword}")]
        [HttpGet]
        public async Task<IList<MessagesView>> SearchByKeyWord([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string keyword, CancellationToken cancellationToken)
        {
            var hasAuthHeader = HttpContext.Request.Headers.ContainsKey("Authorization");
            logger.LogDebug("Received request to /api/messages/search/{Keyword}. Has Authorization header: {HasAuthHeader}", keyword, hasAuthHeader);

            var result = await dataStore.QueryMessages(keyword, pagingInfo, sortInfo, cancellationToken: cancellationToken);
            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);
            return result.Results;
        }

        [Route("endpoints/{endpoint}/messages/search")]
        [HttpGet]
        public async Task<IList<MessagesView>> Search([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string endpoint, string q, CancellationToken cancellationToken)
        {
            var hasAuthHeader = HttpContext.Request.Headers.ContainsKey("Authorization");
            logger.LogDebug("Received request to /api/endpoints/{Endpoint}/messages/search. Has Authorization header: {HasAuthHeader}", endpoint, hasAuthHeader);

            var result = await dataStore.QueryMessagesByReceivingEndpointAndKeyword(endpoint, q, pagingInfo, sortInfo, cancellationToken: cancellationToken);
            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);
            return result.Results;
        }

        [Route("endpoints/{endpoint}/messages/search/{keyword}")]
        [HttpGet]
        public async Task<IList<MessagesView>> SearchByKeyword([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string endpoint, string keyword, CancellationToken cancellationToken)
        {
            var hasAuthHeader = HttpContext.Request.Headers.ContainsKey("Authorization");
            logger.LogDebug("Received request to /api/endpoints/{Endpoint}/messages/search/{Keyword}. Has Authorization header: {HasAuthHeader}", endpoint, keyword, hasAuthHeader);

            var result = await dataStore.QueryMessagesByReceivingEndpointAndKeyword(endpoint, keyword, pagingInfo, sortInfo, cancellationToken: cancellationToken);
            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);
            return result.Results;
        }
    }
}