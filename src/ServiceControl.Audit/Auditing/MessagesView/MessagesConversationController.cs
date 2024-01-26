namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Mvc;
    using Persistence;

    [ApiController]
    [Route("api")]
    public class MessagesConversationController(IAuditDataStore dataStore) : ControllerBase
    {
        [Route("conversations/{conversationId}")]
        [HttpGet]
        public async Task<IList<MessagesView>> Get([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo, string conversationId)
        {
            var result = await dataStore.QueryMessagesByConversationId(conversationId, pagingInfo, sortInfo);
            Response.WithQueryResults(result.QueryStats, pagingInfo);
            return result.Results;
        }
    }
}