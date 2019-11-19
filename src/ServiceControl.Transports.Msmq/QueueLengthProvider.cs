namespace ServiceControl.Transports.Msmq
{
    using System;
    using System.Threading.Tasks;

    class QueueLengthProvider : IProvideQueueLength
    {
        public void Initialize(string connectionString, Action<QueueLengthEntry[], EndpointToQueueMapping> store)
        {
        }

        public void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack)
        {
            //This is a no op for MSMQ since the endpoints report their queue lenght to SC using custom messages
        }

        public Task Start() => Task.CompletedTask;

        public Task Stop() => Task.CompletedTask;
    }
}