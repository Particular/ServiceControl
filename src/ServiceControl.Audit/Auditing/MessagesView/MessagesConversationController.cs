namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;

    class MessagesConversationController : ApiController
    {
        public MessagesConversationController(MessagesByConversationApi api)
        {
            this.api = api;
        }

        [Route("conversations/{conversationid}")]
        public Task<HttpResponseMessage> Get(string conversationid) => api.Execute(this, conversationid);

        readonly MessagesByConversationApi api;
    }
}