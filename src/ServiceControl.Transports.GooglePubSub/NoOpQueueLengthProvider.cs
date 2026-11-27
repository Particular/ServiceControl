namespace ServiceControl.Transports.GooglePubSub;

using System.Threading;
using System.Threading.Tasks;

class NoOpQueueLengthProvider : IProvideQueueLength
{
    public void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack)
    {
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}