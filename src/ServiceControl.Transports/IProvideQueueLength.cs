namespace ServiceControl.Transports
{
    using System;
    using System.Threading.Tasks;

    public interface IProvideQueueLength
    {
        void Initialize(string connectionString, Action<QueueLengthEntry[], EndpointToQueueMapping> store);

        void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack);

        Task Start();

        Task Stop();
    }
}