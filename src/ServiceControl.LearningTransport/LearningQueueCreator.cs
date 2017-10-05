namespace ServiceControl.LearningTransport
{
    using NServiceBus;
    using NServiceBus.Transports;

    class LearningQueueCreator : ICreateQueues
    {
        public void CreateQueueIfNecessary(Address address, string account)
        {
        }
    }
}
