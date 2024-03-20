namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using Persistence.Infrastructure;

    [ApiController]
    [Route("api")]
    public class GetMessagesByConversationController(MessagesByConversationApi byConversationApi)
        : ControllerBase
    {
        [Route("conversations/{conversationId:required:minlength(1)}")]
        [HttpGet]
        public async Task<IList<MessagesView>> Messages([FromQuery] PagingInfo pagingInfo,
            [FromQuery] SortInfo sortInfo,
            [FromQuery(Name = "include_system_messages")]
            bool includeSystemMessages, string conversationId)
        {
            QueryResult<IList<MessagesView>> result = await byConversationApi.Execute(
                new MessagesByConversationContext(pagingInfo, sortInfo, includeSystemMessages, conversationId),
                Request.GetEncodedPathAndQuery());

            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);
            return result.Results;
        }
    }
}