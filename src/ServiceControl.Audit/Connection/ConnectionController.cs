namespace ServiceControl.Audit.Connection
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Infrastructure.Settings;

    public class ConnectionController : ApiController
    {
        readonly Settings settings;

        public ConnectionController(Settings settings) => this.settings = settings;

        [Route("connection")]
        [HttpGet]
        public Task<HttpResponseMessage> GetConnectionDetails() => Task.FromResult(Request.CreateResponse(HttpStatusCode.OK, new
        {
            settings.AuditQueue,
            SagaAudit = new
            {
                SagaAuditQueue = settings.AuditQueue
            }
        }));
    }
}