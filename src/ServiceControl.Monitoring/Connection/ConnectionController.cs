namespace ServiceControl.Monitoring.Connection
{
    using System;
    using System.Text.Json.Serialization;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;

    [ApiController]
    public class ConnectionController(ReceiveAddresses receiveAddresses) : ControllerBase
    {
        readonly string mainInputQueue = receiveAddresses.MainReceiveAddress;
        readonly TimeSpan defaultInterval = TimeSpan.FromSeconds(1);

        [Route("connection")]
        [HttpGet]
        public ActionResult<ConnectionDetails> GetConnectionDetails() =>
            new ConnectionDetails
            {
                Metrics = new MetricsConnectionDetails
                {
                    Enabled = true,
                    MetricsQueue = mainInputQueue,
                    Interval = defaultInterval
                }
            };

        // Backward compatibility reason:
        // to make it so that the latest ServicePulse can talk to ServiceControl 5.0.5
        // the Metrics property must be serialized PascalCase 
        public class ConnectionDetails
        {
            [JsonPropertyName("Metrics")]
            public MetricsConnectionDetails Metrics { get; set; }
        }

        // HINT: This should match the type in the PlatformConnector package
        public class MetricsConnectionDetails
        {
            public bool Enabled { get; set; }
            public string MetricsQueue { get; set; }
            public TimeSpan Interval { get; set; }
        }
    }
}
