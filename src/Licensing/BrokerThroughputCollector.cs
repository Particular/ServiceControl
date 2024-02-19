namespace Particular.License
{
    using Microsoft.Extensions.Logging;

    class BrokerThroughputCollector
    {
        public BrokerThroughputCollector(string broker, ILogger logger)
        {
            this.broker = broker;
            this.logger = logger;
        }

        public void GatherThroughput()
        {
            logger.LogInformation($"Gathering throughput from broker {broker}");
        }

        readonly string broker;
        readonly ILogger logger;
    }
}
