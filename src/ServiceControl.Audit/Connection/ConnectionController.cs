namespace ServiceControl.Audit.Connection
{
    using System.Text.Json;
    using Infrastructure.Settings;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api")]
    public class ConnectionController(Settings settings) : ControllerBase
    {
        [Route("connection")]
        [HttpGet]
        public IActionResult GetConnectionDetails()
        {
            var connectionDetails = new ConnectionDetails
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
            };
            // by default snake case is used for serialization so we take care of explicitly serializing here
            var content = JsonSerializer.Serialize(connectionDetails);
            return Content(content, "application/json");
        }
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