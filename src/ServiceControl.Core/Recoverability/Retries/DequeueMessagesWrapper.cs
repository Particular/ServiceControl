namespace ServiceControl.Recoverability.Retries
{
    using System;
    using System.Threading;
    using NServiceBus;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Transport;

    internal class DequeueMessagesWrapper : IDequeueMessages
    {
        private readonly IDequeueMessages _realDequeuer;
        private int maximumConcurrencyLevel;
        private int disposeSignaled;

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
            Interlocked.Exchange(ref disposeSignaled, 0);
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
            if (Interlocked.Exchange(ref disposeSignaled, 1) != 0)
            {
                return;
            }
            _realDequeuer.Stop();
        }
    }
}