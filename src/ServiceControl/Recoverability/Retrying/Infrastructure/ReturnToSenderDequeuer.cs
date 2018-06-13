namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using Raven.Client;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations.BodyStorage;
    using FailedMessage = ServiceControl.MessageFailures.FailedMessage;

    public class ReturnToSenderDequeuer
    {
        private DequeueMessagesWrapper receiver;
        private Timer timer;
        ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);
        static ILog Log = LogManager.GetLogger(typeof(ReturnToSenderDequeuer));
        bool endedPrematurelly;
        int? targetMessageCount;
        int actualMessageCount;
        Predicate<MessageContext> shouldProcess;
        CaptureIfMessageSendingFails faultManager;
        IBodyStorage bodyStorage;

        public ReturnToSenderDequeuer(IBodyStorage bodyStorage, IDocumentStore store, IDomainEvents domainEvents, string endpointName, Action<EndpointConfiguration> configureTransport)
        {
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
        }

        RawEndpointConfiguration CreateEndpointConfiguration(string endpointName, Action<RawEndpointConfiguration> configureTransport, IErrorHandlingPolicy errorHandlingPolicy)
        {
            var config = RawEndpointConfiguration.Create($"{endpointName}.staging",
                (context, dispatcher) => Handle(context, bodyStorage, dispatcher), "poison" /*TODO*/);

            configureTransport(config);
            config.AutoCreateQueue();
            config.CustomErrorHandlingPolicy(errorHandlingPolicy);

            return config;
        }

        async Task Handle(MessageContext message, IBodyStorage bodyStorage, IDispatchMessages sender)
        {
            if (shouldProcess(message))
            {
                await HandleMessage(message, bodyStorage, sender);

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

        public static async Task HandleMessage(MessageContext message, IBodyStorage bodyStorage, IDispatchMessages sender) //Public for testing
        {
            var body = new byte[0];
            var outgoingHeaders = new Dictionary<string, string>(message.Headers);

            outgoingHeaders.Remove("ServiceControl.Retry.StagingId");

            var messageId = message.MessageId;
            Log.DebugFormat("{0}: Retrieving message body", messageId);

            string attemptMessageId;
            if (outgoingHeaders.TryGetValue("ServiceControl.Retry.Attempt.MessageId", out attemptMessageId))
            {
                Stream stream;
                if (bodyStorage.TryFetch(attemptMessageId, out stream))
                {
                    using (stream)
                    {
                        body = ReadFully(stream);
                    }
                    Log.DebugFormat("{0}: Body size: {1} bytes", messageId, body.LongLength);
                }
                else
                {
                    Log.WarnFormat("{0}: Message Body not found for attempt Id {1}", messageId, attemptMessageId);
                }
                outgoingHeaders.Remove("ServiceControl.Retry.Attempt.MessageId");
            }
            else
            {
                Log.WarnFormat("{0}: Can't find message body. Missing header ServiceControl.Retry.Attempt.MessageId", messageId);
            }

            var outgoingMessage = new OutgoingMessage(messageId, outgoingHeaders, body);

            var destination = outgoingHeaders["ServiceControl.TargetEndpointAddress"];
            Log.DebugFormat("{0}: Forwarding message to {1}", messageId, destination);
            string retryTo;
            if (!outgoingHeaders.TryGetValue("ServiceControl.RetryTo", out retryTo))
            {
                retryTo = destination;
                outgoingHeaders.Remove("ServiceControl.TargetEndpointAddress");
            }
            else
            {
                Log.DebugFormat("{0}: Found ServiceControl.RetryTo header. Rerouting to {1}", messageId, retryTo);
            }

            var transportOp = new TransportOperation(outgoingMessage, new UnicastAddressTag(retryTo));

            await sender.Dispatch(new TransportOperations(transportOp), message.TransportTransaction, message.Extensions)
                .ConfigureAwait(false);
            Log.DebugFormat("{0}: Forwarded message to {1}", messageId, retryTo);
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

        public virtual void Run(Predicate<MessageContext> filter, CancellationToken cancellationToken, int? expectedMessageCount = null)
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

        class CaptureIfMessageSendingFails : IErrorHandlingPolicy
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

            public async Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IDispatchMessages dispatcher)
            {
                try
                {
                    var message = handlingContext.Error.Message;
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
                        reason = handlingContext.Error.Exception.GetBaseException().Message;
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

                return ErrorHandleResult.Handled;
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