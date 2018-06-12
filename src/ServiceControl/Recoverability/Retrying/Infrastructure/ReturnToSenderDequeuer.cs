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
    using ServiceControl.Infrastructure.DomainEvents;
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

        public ReturnToSenderDequeuer(IBodyStorage bodyStorage, ISendMessages sender, IDocumentStore store, IDomainEvents domainEvents, Configure configure)
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

            faultManager = new CaptureIfMessageSendingFails(store, domainEvents, executeOnFailure);
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
                Log.DebugFormat("{0}: Body is NULL", message.Id);
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
            IDocumentStore store;
            IDomainEvents domainEvents;
            readonly Action executeOnFailure;

            public CaptureIfMessageSendingFails(IDocumentStore store, IDomainEvents domainEvents, Action executeOnFailure)
            {
                this.store = store;
                this.executeOnFailure = executeOnFailure;
                this.domainEvents = domainEvents;
            }

            public void SerializationFailedForMessage(TransportMessage message, Exception e)
            {
            }

            public void ProcessingAlwaysFailsForMessage(TransportMessage message, Exception e)
            {
                ProcessingAlwaysFailsForMessageAsync(message, e).GetAwaiter().GetResult();
            }

            private async Task ProcessingAlwaysFailsForMessageAsync(TransportMessage message, Exception e)
            {
                try
                {
                    var destination = message.Headers["ServiceControl.TargetEndpointAddress"];
                    var messageUniqueId = message.Headers["ServiceControl.Retry.UniqueMessageId"];
                    Log.Warn($"Failed to send '{messageUniqueId}' message to '{destination}' for retry. Attempting to revert message status to unresolved so it can be tried again.", e);

                    using (var session = store.OpenAsyncSession())
                    {
                        var failedMessage = await session.LoadAsync<FailedMessage>(FailedMessage.MakeDocumentId(messageUniqueId))
                            .ConfigureAwait(false);
                        if (failedMessage != null)
                        {
                            failedMessage.Status = FailedMessageStatus.Unresolved;
                        }

                        var failedMessageRetry = await session.LoadAsync<FailedMessageRetry>(FailedMessageRetry.MakeDocumentId(messageUniqueId))
                            .ConfigureAwait(false);
                        if (failedMessageRetry != null)
                        {
                            session.Delete(failedMessageRetry);
                        }

                        await session.SaveChangesAsync()
                            .ConfigureAwait(false);
                    }

                    string reason;
                    try
                    {
                        reason = e.GetBaseException().Message;
                    }
                    catch (Exception)
                    {
                        reason = "Failed to retrieve reason!";
                    }

                    await domainEvents.Raise(new MessagesSubmittedForRetryFailed
                    {
                        Reason = reason,
                        FailedMessageId = messageUniqueId,
                        Destination = destination
                    }).ConfigureAwait(false);
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
            IDequeueMessages realDequeuer;
            int maximumConcurrencyLevel;
            object startStopLock = new object();

            public DequeueMessagesWrapper(IDequeueMessages realDequeuer)
            {
                this.realDequeuer = realDequeuer;
            }

            public void Init(Address address, TransactionSettings transactionSettings, Func<TransportMessage, bool> tryProcessMessage, Action<TransportMessage, Exception> endProcessMessage)
            {
                realDequeuer.Init(address, transactionSettings, tryProcessMessage, endProcessMessage);
            }

            public void StartInternal()
            {
                lock (startStopLock)
                {
                    realDequeuer.Start(maximumConcurrencyLevel);
                }
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
                lock (startStopLock)
                {
                    realDequeuer.Stop();
                }
            }
        }
    }
}