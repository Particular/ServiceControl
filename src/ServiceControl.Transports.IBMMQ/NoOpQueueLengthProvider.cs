namespace ServiceControl.Transports.IBMMQ;

using System.Threading;
using System.Threading.Tasks;

class NoOpQueueLengthProvider : IProvideQueueLength
{
    public void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack)
    {
        //This is a no op for MSMQ since the endpoints report their queue length to SC using custom messages
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}