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
                MessageAudit = new MessageAuditConnectionDetails
                {
                    Enabled = true,
                    AuditQueue = settings.AuditQueue
                },
                SagaAudit = new SagaAuditConnectionDetails
                {
                    Enabled = true,
                    SagaAuditQueue = settings.AuditQueue
                }
            },
            jsonSerializerSettings);
    }

    // HINT: This should match the type in the PlatformConnector package
    class MessageAuditConnectionDetails
    {
        public bool Enabled { get; set; }
        public string AuditQueue { get; set; }
    }

    // HINT: This should match the type in the PlatformConnector package
    class SagaAuditConnectionDetails
    {
        public bool Enabled { get; set; }
        public string SagaAuditQueue { get; set; }
    }
}