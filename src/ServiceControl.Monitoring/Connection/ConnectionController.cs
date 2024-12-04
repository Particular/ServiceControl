namespace ServiceControl.Monitoring.Connection
{
    using System;
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;

    [ApiController]
    public class ConnectionController(ReceiveAddresses receiveAddresses) : ControllerBase
    {
        readonly string mainInputQueue = receiveAddresses.MainReceiveAddress;
        readonly TimeSpan defaultInterval = TimeSpan.FromSeconds(1);

        [Route("connection")]
        [HttpGet]
        public IActionResult GetConnectionDetails() =>
            new JsonResult(new ConnectionDetails
            {
                Metrics = new MetricsConnectionDetails
                {
                    Enabled = true,
                    MetricsQueue = mainInputQueue,
                    Interval = defaultInterval
                }
            }, JsonSerializerOptions.Default);

        // ServicePulse expects as result an object with a Metrics root property
        class ConnectionDetails
        {
            public MetricsConnectionDetails Metrics { get; set; }
        }

        // HINT: This should match the type in the PlatformConnector package
        class MetricsConnectionDetails
        {
            public bool Enabled { get; set; }
            public string MetricsQueue { get; set; }
            public TimeSpan Interval { get; set; }
        }
    }
}