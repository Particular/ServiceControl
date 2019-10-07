namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;

    public class GetMessagesController : ApiController
    {
        internal GetMessagesController(GetAllMessagesApi getAllMessagesApi, GetAllMessagesForEndpointApi getAllMessagesForEndpointApi)
        {
            this.getAllMessagesApi = getAllMessagesApi;
            this.getAllMessagesForEndpointApi = getAllMessagesForEndpointApi;
        }

        [Route("messages")]
        [HttpGet]
        public Task<HttpResponseMessage> GetAllMessages() => getAllMessagesApi.Execute(this);

        [Route("endpoints/{endpoint}/messages")]
        [HttpGet]
        public Task<HttpResponseMessage> GetEndpointMessages(string endpoint) => getAllMessagesForEndpointApi.Execute(this, endpoint);

        readonly GetAllMessagesApi getAllMessagesApi;
        readonly GetAllMessagesForEndpointApi getAllMessagesForEndpointApi;
    }
}