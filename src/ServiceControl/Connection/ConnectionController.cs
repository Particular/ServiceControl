namespace ServiceControl.Connection
{
    using System.Threading.Tasks;
    using System.Web.Http;
    using Newtonsoft.Json;

    public class ConnectionController : ApiController
    {
        readonly IPlatformConnectionBuilder connectionBuilder;
        readonly JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings();

        public ConnectionController(IPlatformConnectionBuilder connectionBuilder) => this.connectionBuilder = connectionBuilder;

        [Route("connection")]
        [HttpGet]
        public async Task<IHttpActionResult> GetConnectionDetails()
        {
            var connectionDetails = await connectionBuilder.BuildPlatformConnection().ConfigureAwait(false);

            return Json(
                new
                {
                    settings = connectionDetails.ToDictionary(),
                    errors = connectionDetails.Errors
                },
                jsonSerializerSettings
            );
        }
    }
}
