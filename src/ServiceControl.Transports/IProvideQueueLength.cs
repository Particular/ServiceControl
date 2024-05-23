namespace ServiceControl.Transports
{
    using Microsoft.Extensions.Hosting;

    public interface IProvideQueueLength : IHostedService
    {
        void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack);
    }
}