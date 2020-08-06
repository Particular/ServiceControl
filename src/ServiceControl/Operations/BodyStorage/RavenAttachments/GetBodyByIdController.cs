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

        [Route("messages/{*idCatchAll}")]
        [HttpGet]
        public Task<HttpResponseMessage> CatchAll(string idCatchAll)
        {
            if(!string.IsNullOrEmpty(idCatchAll) && idCatchAll.EndsWith("/body"))
            {
                var id = idCatchAll.Substring(0, idCatchAll.Length - 5);
                return Get(id);
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        }

        readonly GetBodyByIdApi getBodyByIdApi;
    }
}