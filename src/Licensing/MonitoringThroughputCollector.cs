namespace Particular.License
{
    using Microsoft.Extensions.Logging;

    class MonitoringThroughputCollector
    {
        public MonitoringThroughputCollector(ILogger logger)
        {
            this.logger = logger;
        }

        public void GatherThroughput()
        {
            //This may be called as part of the main instance receiving data from monitoring
            logger.LogInformation($"Gathering throughput from Monitoring/");
        }

        readonly ILogger logger;
    }
}
