namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Faults;
    using NServiceBus.Logging;
    using NServiceBus.Satellites;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NServiceBus.Unicast.Transport;
    using Raven.Client;
    using ServiceControl.MessageFailures;
    using FailedMessage = ServiceControl.MessageFailures.FailedMessage;

    public class ReturnToSenderDequeuer : IAdvancedSatellite
    {
        private DequeueMessagesWrapper receiver;
        private Timer timer;
        ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);
        static ILog Log = LogManager.GetLogger(typeof(ReturnToSenderDequeuer));
        bool endedPrematurelly;
        int? targetMessageCount;
        int actualMessageCount;
        Predicate<TransportMessage> shouldProcess; 
        readonly ISendMessages sender;
        CaptureIfMessageSendingFails faultManager;

        public ReturnToSenderDequeuer(ISendMessages sender, IDocumentStore store, IBus bus, Configure configure)
        {
            this.sender = sender;

            Action executeOnFailure = () =>
            {
                if (IsCounting)
                {
                    CountMessageAndStopIfReachedTarget();
                }
                else
                {
                    timer.Change(TimeSpan.FromSeconds(45), Timeout.InfiniteTimeSpan);
                }
            };

            faultManager = new CaptureIfMessageSendingFails(store, bus, executeOnFailure);
            timer = new Timer(state => StopInternal());
            InputAddress = Address.Parse(configure.Settings.EndpointName()).SubScope("staging");
        }

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

        void HandleMessage(TransportMessage message)
        {
            var destination = message.Headers["ServiceControl.TargetEndpointAddress"];

            message.Headers.Remove("ServiceControl.TargetEndpointAddress");
            message.Headers.Remove("ServiceControl.Retry.StagingId");

            try
            {
                sender.Send(message, new SendOptions(destination));
            }
            catch (Exception)
            {
                message.Headers["ServiceControl.TargetEndpointAddress"] = destination;

                throw;
            }
        }

        bool IsCounting => targetMessageCount.HasValue;

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

        public Address InputAddress { get; }
        public bool Disabled => false;

        public Action<TransportReceiver> GetReceiverCustomization()
        {
            return r =>
            {
                receiver = new DequeueMessagesWrapper(r.Receiver);
                r.Receiver = receiver;
                r.FailureManager = faultManager;
            };
        }

        class CaptureIfMessageSendingFails : IManageMessageFailures
        {
            static ILog Log = LogManager.GetLogger(typeof(CaptureIfMessageSendingFails));
            private IDocumentStore store;
            private IBus bus;
            readonly Action executeOnFailure;

            public CaptureIfMessageSendingFails(IDocumentStore store, IBus bus, Action executeOnFailure)
            {
                this.store = store;
                this.bus = bus;
                this.executeOnFailure = executeOnFailure;
            }

            public void SerializationFailedForMessage(TransportMessage message, Exception e)
            {
            }

            public void ProcessingAlwaysFailsForMessage(TransportMessage message, Exception e)
            {
                try
                {
                    var destination = message.Headers["ServiceControl.TargetEndpointAddress"];
                    var messageUniqueId = message.Headers["ServiceControl.Retry.UniqueMessageId"];
                    Log.Warn(string.Format("Failed to send '{0}' message to '{1}' for retry. Attempting to revert message status to unresolved so it can be tried again.", messageUniqueId, destination), e);

                    using (var session = store.OpenSession())
                    {
                        var failedMessage = session.Load<FailedMessage>(FailedMessage.MakeDocumentId(messageUniqueId));
                        if (failedMessage != null)
                        {
                            failedMessage.Status = FailedMessageStatus.Unresolved;
                        }

                        var failedMessageRetry = session.Load<FailedMessageRetry>(FailedMessageRetry.MakeDocumentId(messageUniqueId));
                        if (failedMessageRetry != null)
                        {
                            session.Delete(failedMessageRetry);
                        }

                        session.SaveChanges();
                    }

                    bus.Publish<MessagesSubmittedForRetryFailed>(m =>
                    {
                        m.FailedMessageId = messageUniqueId;
                        m.Destination = destination;
                        try
                        {
                            m.Reason = e.GetBaseException().Message;
                        }
                        catch (Exception)
                        {
                            m.Reason = "Failed to retrieve reason!";
                        }

                    });
                }
                catch (Exception ex)
                {
                    // If something goes wrong here we just ignore, not the end of the world!
                    Log.Error("A failure occurred when trying to handle a retry failure.", ex);
                }
                finally
                {
                    executeOnFailure();
                }
            }

            public void Init(Address address)
            {
            }
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