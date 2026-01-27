namespace ServiceControl.Audit.Connection
{
    using System.Text.Json;
    using Infrastructure.Settings;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api")]
    public class ConnectionController(Settings settings) : ControllerBase
    {
        // This controller doesn't use the default serialization settings because
        // ServicePulse and the Platform Connector Plugin expect the connection
        // details the be serialized and formatted in a specific way
        [Route("connection")]
        [HttpGet]
        public IActionResult GetConnectionDetails() =>
         new JsonResult(
                new ConnectionDetails
                {
                    MessageAudit = new MessageAuditConnectionDetails
                    {
                        Enabled = true,
                        AuditQueue = settings.AuditQueue
                    },
                    SagaAudit = new SagaAuditConnectionDetails
                    {
                        Enabled = true,
                        SagaAuditQueue = settings.AuditQueue,
                    }
                }, JsonSerializerOptions.Default);
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