namespace ServiceControl.CompositeViews.Messages
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;

    class GetMessagesByConversationController : ApiController
    {
        public GetMessagesByConversationController(MessagesByConversationApi messagesByConversationApi)
        {
            this.messagesByConversationApi = messagesByConversationApi;
        }

        [Route("conversations/{conversationid}")]
        [HttpGet]
        public Task<HttpResponseMessage> Messages(string conversationId) => messagesByConversationApi.Execute(this, conversationId);

        readonly MessagesByConversationApi messagesByConversationApi;
    }
}