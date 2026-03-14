namespace ServiceControl.Transports.RabbitMQ
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    // Used when DisableBrokerRequirementChecks=true and the RabbitMQ Management API is not available
    sealed class NoOpQueueLengthProvider(ILogger<NoOpQueueLengthProvider> logger) : IProvideQueueLength
    {
        public void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack)
        {
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogWarning("Queue length monitoring is disabled because RabbitMQ broker requirement checks are disabled via the connection string. Queue length data will not be available.");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
