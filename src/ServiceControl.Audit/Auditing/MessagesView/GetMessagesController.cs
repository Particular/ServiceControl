namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Mvc;
    using Persistence;

    // All routes matching `messages/*` must be in this controller as WebAPI cannot figure out the overlapping routes
    // from `messages/{*catchAll}` if they're in separate controllers.
    [ApiController]
    [Route("api")]
    public class GetMessagesController(IAuditDataStore dataStore) : ControllerBase
    {
        [Route("messages")]
        [HttpGet]
        public async Task<IList<MessagesView>> GetAllMessages([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, [FromQuery(Name = "include_system_messages")] bool includeSystemMessages)
        {
            var result = await dataStore.GetMessages(includeSystemMessages, pagingInfo, sortInfo);
            Response.WithQueryResults(result.QueryStats, pagingInfo);
            return result.Results;
        }

        [Route("endpoints/{endpoint}/messages")]
        [HttpGet]
        public async Task<IList<MessagesView>> GetEndpointMessages([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, [FromQuery(Name = "include_system_messages")] bool includeSystemMessages, string endpoint)
        {
            var result = await dataStore.QueryMessagesByReceivingEndpoint(includeSystemMessages, endpoint, pagingInfo, sortInfo);
            Response.WithQueryResults(result.QueryStats, pagingInfo);
            return result.Results;
        }

        [Route("endpoints/{endpoint}/audit-count")]
        [HttpGet]
        public async Task<IList<AuditCount>> GetEndpointAuditCounts([FromQuery] PagingInfo pagingInfo, string endpoint)
        {
            var result = await dataStore.QueryAuditCounts(endpoint);
            Response.WithQueryResults(result.QueryStats, pagingInfo);
            return result.Results;
        }

        [Route("messages/{id}/body")]
        [HttpGet]
        public async Task<IActionResult> Get(string id)
        {
            var messageId = id;
            messageId = messageId?.Replace("/", @"\");

            var result = await dataStore.GetMessageBody(messageId);

            if (result.Found == false)
            {
                return NotFound();
            }

            if (result.HasContent == false)
            {
                return NoContent();
            }

            // TODO: Verify that this is the correct way to handle this
            OkObjectResult content;
            if (result.StringContent != null)
            {
                content = new OkObjectResult(result.StringContent);
            }
            else if (result.StreamContent != null)
            {
                content = new OkObjectResult(result.StreamContent);
            }
            else
            {
                throw new Exception($"Metadata for message '{messageId}' indicated that a body was present but no content could be found in storage");
            }

            Response.Headers.ContentType = result.ContentType ?? "text/*";
            Response.Headers.ContentLength = result.ContentLength;
            Response.Headers.ETag = result.ETag;
            return content;
        }

        // TODO: Verify if this catch all approach is still relevant today with Kestrel
        // Possible a message may contain a slash or backslash, either way http.sys will rewrite it to forward slash,
        // and then the "normal" route above will not activate, resulting in 404 if this route is not present.
        [Route("messages/{*catchAll}")]
        [HttpGet]
        public async Task<IActionResult> CatchAll(string catchAll)
        {
            if (!string.IsNullOrEmpty(catchAll) && catchAll.EndsWith("/body"))
            {
                var id = catchAll.Substring(0, catchAll.Length - 5);
                return await Get(id);
            }

            return NotFound();
        }

        [Route("messages/search")]
        [HttpGet]
        public async Task<IList<MessagesView>> Search([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string q)
        {
            var result = await dataStore.QueryMessages(q, pagingInfo, sortInfo);
            Response.WithQueryResults(result.QueryStats, pagingInfo);
            return result.Results;
        }

        [Route("messages/search/{keyword}")]
        [HttpGet]
        public async Task<IList<MessagesView>> SearchByKeyWord([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string keyword)
        {
            var result = await dataStore.QueryMessages(keyword?.Replace("/", @"\"), pagingInfo, sortInfo);
            Response.WithQueryResults(result.QueryStats, pagingInfo);
            return result.Results;
        }

        [Route("endpoints/{endpoint}/messages/search")]
        [HttpGet]
        public async Task<IList<MessagesView>> Search([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string endpoint, string q)
        {
            var result = await dataStore.QueryMessagesByReceivingEndpointAndKeyword(endpoint, q, pagingInfo, sortInfo);
            Response.WithQueryResults(result.QueryStats, pagingInfo);
            return result.Results;
        }

        [Route("endpoints/{endpoint}/messages/search/{keyword}")]
        [HttpGet]
        public async Task<IList<MessagesView>> SearchByKeyword([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string endpoint, string keyword)
        {
            var result = await dataStore.QueryMessagesByReceivingEndpointAndKeyword(endpoint, keyword, pagingInfo, sortInfo);
            Response.WithQueryResults(result.QueryStats, pagingInfo);
            return result.Results;
        }
    }
}