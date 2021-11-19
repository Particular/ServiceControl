namespace ServiceControl.Audit.Connection
{
    using System.Web.Http;
    using Infrastructure.Settings;
    using Newtonsoft.Json;

    public class ConnectionController : ApiController
    {
        readonly Settings settings;
        readonly JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings();

        public ConnectionController(Settings settings) => this.settings = settings;

        [Route("connection")]
        [HttpGet]
        public IHttpActionResult GetConnectionDetails() =>
            Json(new
            {
                MessageAudit = new
                {
                    settings.AuditQueue
                },
                SagaAudit = new
                {
                    SagaAuditQueue = settings.AuditQueue
                }
            },
            jsonSerializerSettings);
    }
}