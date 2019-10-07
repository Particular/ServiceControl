namespace ServiceControl.CompositeViews.Messages
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;

    public class GetMessagesByQueryController : ApiController
    {
        internal GetMessagesByQueryController(SearchApi searchApi, SearchEndpointApi searchEndpointApi)
        {
            this.searchEndpointApi = searchEndpointApi;
            this.searchApi = searchApi;
        }

        [Route("messages/search")]
        [HttpGet]
        public Task<HttpResponseMessage> Search(string q) => searchApi.Execute(this, q);

        [Route("messages/search/{keyword}")]
        [HttpGet]
        public Task<HttpResponseMessage> SearchByKeyWord(string keyword) => searchApi.Execute(this, keyword?.Replace("/", @"\"));

        [Route("endpoints/{endpoint}/messages/search")]
        [HttpGet]
        public Task<HttpResponseMessage> Search(string endpoint, string q) => searchEndpointApi.Execute(this, new SearchEndpointApi.Input
        {
            Endpoint = endpoint,
            Keyword = q
        });

        [Route("endpoints/{endpoint}/messages/search/{keyword}")]
        [HttpGet]
        public Task<HttpResponseMessage> SearchByKeyword(string endpoint, string keyword) => searchEndpointApi.Execute(this, new SearchEndpointApi.Input
        {
            Endpoint = endpoint,
            Keyword = keyword
        });


        readonly SearchApi searchApi;
        readonly SearchEndpointApi searchEndpointApi;
    }
}