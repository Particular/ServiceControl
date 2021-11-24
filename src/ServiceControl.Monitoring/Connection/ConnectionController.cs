namespace ServiceControl.Monitoring.Connection
{
    using System;
    using System.Web.Http;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.Settings;

    public class ConnectionController : ApiController
    {
        readonly JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings();
        readonly string mainInputQueue;
        readonly TimeSpan defaultInterval = TimeSpan.FromSeconds(1);

        public ConnectionController(ReadOnlySettings nsbSettings) => mainInputQueue = nsbSettings.LocalAddress();

        [Route("connection")]
        [HttpGet]
        public IHttpActionResult GetConnectionDetails() =>
            Json(
                new
                {
                    Metrics = new MetricsConnectionDetails
                    {
                        Enabled = true,
                        MetricsQueue = mainInputQueue,
                        Interval = defaultInterval
                    }
                },
                jsonSerializerSettings
        );

        // HINT: This should match the type in the PlatformConnector package
        class MetricsConnectionDetails
        {
            public bool Enabled { get; set; }
            public string MetricsQueue { get; set; }
            public TimeSpan Interval { get; set; }
        }
    }
}
