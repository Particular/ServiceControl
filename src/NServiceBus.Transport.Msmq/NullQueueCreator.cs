namespace NServiceBus.Transport.Msmq
{
    using System.Threading.Tasks;

    class NullQueueCreator : ICreateQueues
    {
        public Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            return Task.FromResult(0);
        }
    }
}