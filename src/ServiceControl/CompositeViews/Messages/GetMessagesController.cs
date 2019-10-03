namespace ServiceControl.CompositeViews.Messages
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;

    public class GetMessagesController : ApiController
    {
        internal GetMessagesController(GetAllMessagesApi getAllMessagesApi, GetAllMessagesForEndpointApi getAllMessagesForEndpointApi)
        {
            this.getAllMessagesForEndpointApi = getAllMessagesForEndpointApi;
            this.getAllMessagesApi = getAllMessagesApi;
        }

        [Route("messages")]
        [HttpGet]
        public Task<HttpResponseMessage> Messages() => getAllMessagesApi.Execute(this, NoInput.Instance);

        [Route("endpoints/{endpoint}/messages")]
        [HttpGet]
        public Task<HttpResponseMessage> MessagesForEndpoint(string endpoint) => getAllMessagesForEndpointApi.Execute(this, endpoint);

        GetAllMessagesApi getAllMessagesApi;
        GetAllMessagesForEndpointApi getAllMessagesForEndpointApi;
    }
}