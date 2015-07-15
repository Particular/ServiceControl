namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Satellites;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Transport;

    abstract class AdvancedDequeuer : IAdvancedSatellite
    {
        public static Address Address = Address.Parse(Configure.EndpointName).SubScope("staging");
        private DequeueMessagesWrapper receiver;
        private Timer timer;
        ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);
        static ILog Log = LogManager.GetLogger(typeof(AdvancedDequeuer));

        protected AdvancedDequeuer()
        {
            timer = new Timer(state => StopInternal());
        }

        protected abstract void HandleMessage(TransportMessage message);

        public bool Handle(TransportMessage message)
        {
            HandleMessage(message);
            timer.Change(TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
            return true;
        }

        public void Start()
        {
        }

        public void Run()
        {
            try
            {
                resetEvent.Reset();
                receiver.StartInternal();
                timer.Change(TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
                Log.InfoFormat("{0} started", GetType().Name);
            }
            finally
            {
                resetEvent.Wait();
            }
        }

        public void Stop()
        {
            StopInternal();
        }

        void StopInternal()
        {
            receiver.StopInternal();
            resetEvent.Set();
            Log.InfoFormat("{0} stopped", GetType().Name);
        }

        public Address InputAddress { get { return Address; } }
        public bool Disabled { get { return false; } }
        public Action<TransportReceiver> GetReceiverCustomization()
        {
            return r =>
            {
                receiver = new DequeueMessagesWrapper(r.Receiver);
                r.Receiver = receiver;
            };
        }

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
}