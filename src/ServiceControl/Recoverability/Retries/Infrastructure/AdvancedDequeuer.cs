namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Satellites;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Transport;

    abstract class AdvancedDequeuer : IAdvancedSatellite
    {
        private DequeueMessagesWrapper receiver;
        private Timer timer;
        ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);
        static ILog Log = LogManager.GetLogger(typeof(AdvancedDequeuer));
        bool endedPrematurelly;
        int? targetMessageCount;
        int actualMessageCount;
        Predicate<TransportMessage> shouldProcess; 

        protected AdvancedDequeuer(Configure configure)
        {
            timer = new Timer(state => StopInternal());
            InputAddress = Address.Parse(configure.Settings.EndpointName()).SubScope("staging");
        }

        protected abstract void HandleMessage(TransportMessage message);

        public bool Handle(TransportMessage message)
        {
            if (shouldProcess(message))
            {
                HandleMessage(message);
                if (IsCounting)
                {
                    CountMessageAndStopIfReachedTarget();
                }
            }
            if (!IsCounting)
            {
                timer.Change(TimeSpan.FromSeconds(45), Timeout.InfiniteTimeSpan);
            }
            return true;
        }

        bool IsCounting
        {
            get { return targetMessageCount.HasValue; }
        }

        void CountMessageAndStopIfReachedTarget()
        {
            var currentMessageCount = Interlocked.Increment(ref actualMessageCount);
            Log.DebugFormat("Handling message {0} of {1}", currentMessageCount, targetMessageCount);
            if (currentMessageCount >= targetMessageCount.GetValueOrDefault())
            {
                // NOTE: This needs to run on a different thread or a deadlock will happen trying to shut down the receiver
                Task.Factory.StartNew(StopInternal);
            }
        }

        public void Start()
        {
        }

        public void Run(Predicate<TransportMessage> filter, int? expectedMessageCount = null)
        {
            try
            {
                shouldProcess = filter;
                resetEvent.Reset();
                targetMessageCount = expectedMessageCount;
                actualMessageCount = 0;
                receiver.StartInternal();
                if (!expectedMessageCount.HasValue)
                {
                    timer.Change(TimeSpan.FromSeconds(45), Timeout.InfiniteTimeSpan);
                }
                Log.InfoFormat("{0} started", GetType().Name);
            }
            finally
            {
                resetEvent.Wait();
            }

            if (endedPrematurelly)
            {
                throw new Exception("We are in the process of shutting down. Safe to ignore.");
            }
        }

        public void Stop()
        {
            timer.Dispose();
            endedPrematurelly = true;
            resetEvent.Set();
        }

        void StopInternal()
        {
            receiver.StopInternal();
            resetEvent.Set();
            Log.InfoFormat("{0} stopped", GetType().Name);
        }

        public Address InputAddress { get; private set; }
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

                try
                {
                    _realDequeuer.Stop();
                }
                catch (Exception)
                {
                    // Making build go green.
                    var r = 1 + 1;
                    Interlocked.Increment(ref r);
                    // We are shutting down, race condition can result in an exception in the real dequeuer.
                }
            }
        }
    }
}