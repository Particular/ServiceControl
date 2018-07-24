namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.DomainEvents;
    using MessageFailures;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using Raven.Client;

    public class ReturnToSenderDequeuer
    {
        public ReturnToSenderDequeuer(TransportDefinition transportDefinition, ReturnToSender returnToSender, IDocumentStore store, IDomainEvents domainEvents, string endpointName, RawEndpointFactory rawEndpointFactory)
        {
            InputAddress = $"{endpointName}.staging";
            this.returnToSender = returnToSender;

            createEndpointConfiguration = () =>
            {
                var config = rawEndpointFactory.CreateRawEndpointConfiguration(InputAddress, Handle, transportDefinition);

                config.CustomErrorHandlingPolicy(faultManager);

                return config;
            };

            faultManager = new CaptureIfMessageSendingFails(store, domainEvents, IncrementCounterOrProlongTimer);
            timer = new Timer(state => StopInternal().GetAwaiter().GetResult());
        }

        public string InputAddress { get; }

        bool IsCounting => targetMessageCount.HasValue;

        async Task Handle(MessageContext message, IDispatchMessages sender)
        {
            if (Log.IsDebugEnabled)
            {
                var stagingId = message.Headers["ServiceControl.Retry.StagingId"];
                Log.DebugFormat("Handling message with id {0} and staging id {1} in input queue {2}", message.MessageId, stagingId, InputAddress);
            }

            if (shouldProcess(message))
            {
                await returnToSender.HandleMessage(message, sender);
                await IncrementCounterOrProlongTimer();
            }
            else
            {
                Log.WarnFormat("Rejecting message from staging queue as it's not part of a fully staged batch: {0}", message.MessageId);
            }
        }

        Task IncrementCounterOrProlongTimer()
        {
            if (IsCounting)
            {
                return CountMessageAndStopIfReachedTarget();
            }

            Log.Debug("Resetting timer");
            timer.Change(TimeSpan.FromSeconds(45), Timeout.InfiniteTimeSpan);
            return TaskEx.CompletedTask;
        }

        Task CountMessageAndStopIfReachedTarget()
        {
            var currentMessageCount = Interlocked.Increment(ref actualMessageCount);
            Log.DebugFormat("Handling message {0} of {1}", currentMessageCount, targetMessageCount);
            if (currentMessageCount >= targetMessageCount.GetValueOrDefault())
            {
                Log.DebugFormat("Target count reached. Shutting down forwarder");
                return StopInternal();
            }

            return TaskEx.CompletedTask;
        }

        public Task CreateQueue()
        {
            var config = createEndpointConfiguration();
            return RawEndpoint.Create(config);
        }

        public virtual async Task Run(Predicate<MessageContext> filter, CancellationToken cancellationToken, int? expectedMessageCount = null)
        {
            IReceivingRawEndpoint processor = null;
            CancellationTokenRegistration? registration = null;
            try
            {
                Log.DebugFormat("Started. Expectected message count {0}", expectedMessageCount);

                if (expectedMessageCount.HasValue && expectedMessageCount.Value == 0)
                {
                    return;
                }

                shouldProcess = filter;
                targetMessageCount = expectedMessageCount;
                actualMessageCount = 0;
                Log.DebugFormat("Starting receiver");

                var config = createEndpointConfiguration();
                syncEvent = new TaskCompletionSource<bool>();
                stopCompletionSource = new TaskCompletionSource<bool>();
                registration = cancellationToken.Register(() => { Task.Run(() => syncEvent.TrySetResult(true), CancellationToken.None).Ignore(); });

                processor = await RawEndpoint.Start(config).ConfigureAwait(false);

                if (!expectedMessageCount.HasValue)
                {
                    Log.Debug("Running in timeout mode. Starting timer");
                    timer.Change(TimeSpan.FromSeconds(45), Timeout.InfiniteTimeSpan);
                }

                Log.InfoFormat("{0} started", GetType().Name);
            }
            finally
            {
                Log.DebugFormat("Waiting for {0} finish", GetType().Name);
                await syncEvent.Task.ConfigureAwait(false);
                registration?.Dispose();
                if (processor != null)
                {
                    await processor.Stop().ConfigureAwait(false);
                }

                await Task.Run(() => stopCompletionSource.TrySetResult(true), CancellationToken.None);
                Log.DebugFormat("{0} finished", GetType().Name);
            }

            if (endedPrematurelly || cancellationToken.IsCancellationRequested)
            {
                throw new Exception("We are in the process of shutting down. Safe to ignore.");
            }
        }

        public Task Stop()
        {
            timer.Dispose();
            endedPrematurelly = true;
            return StopInternal();
        }

        async Task StopInternal()
        {
            // NOTE: This needs to run on a different thread or a deadlock will happen trying to shut down the receiver
            await Task.Run(() => syncEvent?.TrySetResult(true)).ConfigureAwait(false);
            await (stopCompletionSource?.Task ?? (Task)Task.FromResult(0)).ConfigureAwait(false);
            Log.InfoFormat("{0} stopped", GetType().Name);
        }

        Timer timer;
        TaskCompletionSource<bool> syncEvent;
        TaskCompletionSource<bool> stopCompletionSource;
        private bool endedPrematurelly;
        int? targetMessageCount;
        int actualMessageCount;
        Predicate<MessageContext> shouldProcess;
        CaptureIfMessageSendingFails faultManager;
        Func<RawEndpointConfiguration> createEndpointConfiguration;
        private ReturnToSender returnToSender;
        static ILog Log = LogManager.GetLogger(typeof(ReturnToSenderDequeuer));

        class CaptureIfMessageSendingFails : IErrorHandlingPolicy
        {
            public CaptureIfMessageSendingFails(IDocumentStore store, IDomainEvents domainEvents, Func<Task> executeOnFailure)
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
                    Log.Warn($"Failed to send '{messageUniqueId}' message to '{destination}' for retry. Attempting to revert message status to unresolved so it can be tried again.", handlingContext.Error.Exception);

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
                    await executeOnFailure();
                }

                return ErrorHandleResult.Handled;
            }

            readonly Func<Task> executeOnFailure;
            IDocumentStore store;
            IDomainEvents domainEvents;
            static ILog Log = LogManager.GetLogger(typeof(CaptureIfMessageSendingFails));
        }
    }
}