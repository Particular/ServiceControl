namespace ServiceControl.Transports.Msmq
{
    using System.Threading.Tasks;

    class QueueLengthProvider : IProvideQueueLength
    {
        public void Initialize(string connectionString, QueueLengthStoreDto store)
        {
        }

        public void TrackEndpointInputQueue(string endpointName, string queueAddress)
        {
            //This is a no op for MSMQ since the endpoints report their queue lenght to SC using custom messages
        }

        public Task Start() => Task.CompletedTask;

        public Task Stop() => Task.CompletedTask;
    }
}