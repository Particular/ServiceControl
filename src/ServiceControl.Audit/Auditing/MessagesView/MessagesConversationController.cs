namespace ServiceControl.Audit.Auditing.MessagesView
{
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
    public class MessagesConversationController(IAuditDataStore dataStore, ILogger<MessagesConversationController> logger) : ControllerBase
    {
        [Route("conversations/{conversationId}")]
        [HttpGet]
        public async Task<IList<MessagesView>> Get([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string conversationId, CancellationToken cancellationToken)
        {
            var hasAuthHeader = HttpContext.Request.Headers.ContainsKey("Authorization");
            logger.LogDebug("Received request to /api/conversations/{ConversationId}. Has Authorization header: {HasAuthHeader}", conversationId, hasAuthHeader);

            var result = await dataStore.QueryMessagesByConversationId(conversationId, pagingInfo, sortInfo, cancellationToken);
            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);
            return result.Results;
        }
    }
}