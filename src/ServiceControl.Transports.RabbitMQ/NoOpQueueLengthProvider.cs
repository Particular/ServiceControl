namespace ServiceControl.Transports.RabbitMQ
{
    using System.Threading;
    using System.Threading.Tasks;

    class NoOpQueueLengthProvider : IProvideQueueLength
    {
        public void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack)
        {
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
