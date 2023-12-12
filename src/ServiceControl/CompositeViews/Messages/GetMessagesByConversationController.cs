namespace ServiceControl.CompositeViews.Messages
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Microsoft.AspNetCore.Mvc;
    using Persistence.Infrastructure;

    class GetMessagesByConversationController : ControllerBase
    {
        public GetMessagesByConversationController(MessagesByConversationApi messagesByConversationApi)
        {
            this.messagesByConversationApi = messagesByConversationApi;
        }

        [Route("conversations/{conversationid}")]
        [HttpGet]
        public Task<IActionResult> Messages([FromQuery] PagingInfo pageInfo, string conversationId)
        {
            return messagesByConversationApi.Execute(this, conversationId);
        }

        readonly MessagesByConversationApi messagesByConversationApi;
    }
}