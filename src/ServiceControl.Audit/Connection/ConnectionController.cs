namespace ServiceControl.Audit.Connection
{
    using Infrastructure.Settings;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api")]
    public class ConnectionController : ControllerBase
    {
        readonly Settings settings;

        public ConnectionController(Settings settings) => this.settings = settings;

        [Route("connection")]
        [HttpGet]
        public ConnectionDetails GetConnectionDetails() =>
            new()
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
            };
    }

    public class ConnectionDetails
    {
        public MessageAuditConnectionDetails MessageAudit { get; set; }
        public SagaAuditConnectionDetails SagaAudit { get; set; }

    }

    // HINT: This should match the type in the PlatformConnector package
    public class MessageAuditConnectionDetails
    {
        public bool Enabled { get; set; }
        public string AuditQueue { get; set; }
    }

    // HINT: This should match the type in the PlatformConnector package
    public class SagaAuditConnectionDetails
    {
        public bool Enabled { get; set; }
        public string SagaAuditQueue { get; set; }
    }
}