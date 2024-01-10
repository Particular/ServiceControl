namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Persistence.Infrastructure;

    [ApiController]
    [Route("api")]
    public class GetMessagesByConversationController(MessagesByConversationApi byConversationApi)
        : ControllerBase
    {
        [Route("conversations/{conversationid}")]
        [HttpGet]
        public Task<IList<MessagesView>> Messages([FromQuery] PagingInfo pagingInfo, [FromQuery] SortInfo sortInfo,
            [FromQuery(Name = "include_system_messages")] bool includeSystemMessages, string conversationId) =>
            byConversationApi.Execute(new(pagingInfo, sortInfo, includeSystemMessages, conversationId));
    }
}