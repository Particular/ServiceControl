namespace Particular.License
{
    using Microsoft.Extensions.Logging;

    class AuditThroughputCollector
    {
        public AuditThroughputCollector(string serviceControlAPI, ILogger logger)
        {
            this.serviceControlAPI = serviceControlAPI;
            this.logger = logger;
        }

        public void GatherThroughput()
        {
            logger.LogInformation($"Gathering throughput from Audit using {serviceControlAPI}");
        }

        readonly string serviceControlAPI;
        readonly ILogger logger;
    }
}
