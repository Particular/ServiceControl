namespace ServiceControl.Operations.BodyStorage.Api
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;

    public class GetBodyByIdController : ApiController
    {
        internal GetBodyByIdController(GetBodyByIdApi getBodyByIdApi)
        {
            this.getBodyByIdApi = getBodyByIdApi;
        }

        [Route("messages/{id}/body")]
        [HttpGet]
        public Task<HttpResponseMessage> Get(string id) => getBodyByIdApi.Execute(this, id);

        readonly GetBodyByIdApi getBodyByIdApi;
    }
}