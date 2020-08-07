namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;

    public class GetMessagesController : ApiController
    {
        internal GetMessagesController(GetAllMessagesApi getAllMessagesApi, GetAllMessagesForEndpointApi getAllMessagesForEndpointApi, GetBodyByIdApi getBodyByIdApi)
        {
            this.getAllMessagesApi = getAllMessagesApi;
            this.getAllMessagesForEndpointApi = getAllMessagesForEndpointApi;
            this.getBodyByIdApi = getBodyByIdApi;
        }

        [Route("messages")]
        [HttpGet]
        public Task<HttpResponseMessage> GetAllMessages() => getAllMessagesApi.Execute(this);

        [Route("endpoints/{endpoint}/messages")]
        [HttpGet]
        public Task<HttpResponseMessage> GetEndpointMessages(string endpoint) => getAllMessagesForEndpointApi.Execute(this, endpoint);

        [Route("messages/{id}/body")]
        [HttpGet]
        public Task<HttpResponseMessage> Get(string id) => getBodyByIdApi.Execute(Request, id);

        // Possible a message may contain a slash or backslash, either way http.sys will rewrite it to forward slash,
        // and then the "normal" route above will not activate, resulting in 404 if this route is not present.
        [Route("messages/{*catchAll}")]
        [HttpGet]
        public Task<HttpResponseMessage> CatchAll(string catchAll)
        {
            if (!string.IsNullOrEmpty(catchAll) && catchAll.EndsWith("/body"))
            {
                var id = catchAll.Substring(0, catchAll.Length - 5);
                return Get(id);
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        }

        readonly GetAllMessagesApi getAllMessagesApi;
        readonly GetAllMessagesForEndpointApi getAllMessagesForEndpointApi;
        readonly GetBodyByIdApi getBodyByIdApi;
    }
}