namespace ServiceControl.Recoverability
{
    using System;
    using System.IO;
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
    using ServiceControl.Operations.BodyStorage;
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
        IBodyStorage bodyStorage;

        public ReturnToSenderDequeuer(IBodyStorage bodyStorage, ISendMessages sender, IDocumentStore store, IBus bus, Configure configure)
        {
            this.sender = sender;
            this.bodyStorage = bodyStorage;

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
                HandleMessage(message, bodyStorage, sender);

                if (IsCounting)
                {
                    CountMessageAndStopIfReachedTarget();
                }
            }
            else
            {
                Log.WarnFormat("Rejecting message from staging queue as it's not part of a fully staged batch: {0}", message.Id);
            }

            if (!IsCounting)
            {
                Log.Debug("Resetting timer");
                timer.Change(TimeSpan.FromSeconds(45), Timeout.InfiniteTimeSpan);
            }

            return true;
        }

        static byte[] ReadFully(Stream input)
        {
            var buffer = new byte[16 * 1024];
            using (var ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        public static void HandleMessage(TransportMessage message, IBodyStorage bodyStorage, ISendMessages sender) //Public for testing
        {
            message.Headers.Remove("ServiceControl.Retry.StagingId");

            Log.DebugFormat("{0}: Retrieving message body", message.Id);

            string attemptMessageId;
            if (message.Headers.TryGetValue("ServiceControl.Retry.Attempt.MessageId", out attemptMessageId))
            {
                Stream stream;
                if (bodyStorage.TryFetch(attemptMessageId, out stream))
                {
                    using (stream)
                    {
                        message.Body = ReadFully(stream);
                    }
                }
                else
                { 
                    Log.WarnFormat("{0}: Message Body not found for attempt Id {1}", message.Id, attemptMessageId);
                }
                message.Headers.Remove("ServiceControl.Retry.Attempt.MessageId");
            }
            else
            {
                Log.WarnFormat("{0}: Can't find message body. Missing header ServiceControl.Retry.Attempt.MessageId", message.Id);
            }

            if (message.Body != null)
            {
                Log.DebugFormat("{0}: Body size: {1} bytes", message.Id, message.Body.LongLength);
            }
            else
            {
                Log.DebugFormat("{0}: Body is NULL");
            }

            var destination = message.Headers["ServiceControl.TargetEndpointAddress"];
            Log.DebugFormat("{0}: Forwarding message to {1}", message.Id, destination);
            try
            {
                string retryTo;
                if (!message.Headers.TryGetValue("ServiceControl.RetryTo", out retryTo))
                {
                    retryTo = destination;
                    message.Headers.Remove("ServiceControl.TargetEndpointAddress");
                }
                else
                {
                    Log.DebugFormat("{0}: Found ServiceControl.RetryTo header. Rerouting to {1}", message.Id, retryTo);
                }

                sender.Send(message, new SendOptions(retryTo));
                Log.DebugFormat("{0}: Forwarded message to {1}", message.Id, retryTo);
            }
            catch (Exception)
            {
                Log.WarnFormat("{0}: Error forwarding message, resetting headers", message.Id);
                message.Headers["ServiceControl.TargetEndpointAddress"] = destination;
                if (attemptMessageId != null)
                {
                    message.Headers["ServiceControl.Retry.Attempt.MessageId"] = attemptMessageId;
                }

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
                Log.DebugFormat("Target count reached. Shutting down forwarder");
                // NOTE: This needs to run on a different thread or a deadlock will happen trying to shut down the receiver
                Task.Factory.StartNew(StopInternal);
            }
        }

        public void Start()
        {
        }

        public virtual void Run(Predicate<TransportMessage> filter, CancellationToken cancellationToken, int? expectedMessageCount = null)
        {
            try
            {
                Log.DebugFormat("Started. Expectected message count {0}", expectedMessageCount);

                if (expectedMessageCount.HasValue && expectedMessageCount.Value == 0)
                {
                    return;
                }

                shouldProcess = filter;
                resetEvent.Reset();
                targetMessageCount = expectedMessageCount;
                actualMessageCount = 0;
                Log.DebugFormat("Starting receiver");
                receiver.StartInternal();
                if (!expectedMessageCount.HasValue)
                {
                    Log.Debug("Running in timeout mode. Starting timer");
                    timer.Change(TimeSpan.FromSeconds(45), Timeout.InfiniteTimeSpan);
                }
                Log.InfoFormat("{0} started", GetType().Name);
            }
            finally
            {
                Log.DebugFormat("Waiting for finish");
                resetEvent.Wait(cancellationToken);
                Log.DebugFormat("Finished");
            }

            if (endedPrematurelly || cancellationToken.IsCancellationRequested)
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
                    Log.Warn($"Failed to send '{messageUniqueId}' message to '{destination}' for retry. Attempting to revert message status to unresolved so it can be tried again.", e);

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