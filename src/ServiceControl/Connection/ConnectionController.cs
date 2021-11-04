namespace ServiceControl.Connection
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;

    public class ConnectionController : ApiController
    {
        readonly IPlatformConnectionBuilder connectionBuilder;

        public ConnectionController(IPlatformConnectionBuilder connectionBuilder) => this.connectionBuilder = connectionBuilder;

        [Route("connection")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetConnectionDetails()
        {
            var connectionDetails = await connectionBuilder.BuildPlatformConnection().ConfigureAwait(false);

            return Request.CreateResponse(
                HttpStatusCode.OK,
                connectionDetails.ToDictionary()
            );
        }
    }
}
