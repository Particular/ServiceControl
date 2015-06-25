namespace ServiceControl.Recoverability.Retries
{
    using System;
    using NServiceBus;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Transport;

    internal class DequeueMessagesWrapper : IDequeueMessages
    {
        private readonly IDequeueMessages _realDequeuer;
        private int maximumConcurrencyLevel;

        public DequeueMessagesWrapper(IDequeueMessages realDequeuer)
        {
            _realDequeuer = realDequeuer;
        }

        public void Init(Address address, TransactionSettings transactionSettings, Func<TransportMessage, bool> tryProcessMessage, Action<TransportMessage, Exception> endProcessMessage)
        {
            _realDequeuer.Init(address, transactionSettings, tryProcessMessage, endProcessMessage);
        }

        public void StartInternal()
        {
            _realDequeuer.Start(maximumConcurrencyLevel);
        }

        public void Start(int maximumConcurrencyLevel)
        {
            this.maximumConcurrencyLevel = maximumConcurrencyLevel;
        }

        public void Stop()
        {
        }

        public void StopInternal()
        {
            _realDequeuer.Stop();
        }
    }
}