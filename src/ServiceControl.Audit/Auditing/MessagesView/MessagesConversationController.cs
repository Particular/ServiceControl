namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api")]
    public class MessagesConversationController : ControllerBase
    {
        public MessagesConversationController(MessagesByConversationApi api)
        {
            this.api = api;
        }

        [Route("conversations/{conversationid}")]
        [HttpGet]
        public Task<HttpResponseMessage> Get(string conversationid) => api.Execute(this, conversationid);

        readonly MessagesByConversationApi api;
    }
}