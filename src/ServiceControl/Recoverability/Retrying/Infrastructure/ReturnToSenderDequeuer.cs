namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Transports;

    class ReturnToSenderDequeuer : IHostedService
    {
        public ReturnToSenderDequeuer(ReturnToSender returnToSender, IErrorMessageDataStore dataStore, IDomainEvents domainEvents, ITransportCustomization transportCustomization, TransportSettings transportSettings, Settings settings)
        {
            InputAddress = transportCustomization.ToTransportQualifiedQueueName(settings.StagingQueue);
            this.returnToSender = returnToSender;
            errorQueue = settings.ErrorQueue;
            this.transportCustomization = transportCustomization;
            this.transportSettings = transportSettings;

            faultManager = new CaptureIfMessageSendingFails(dataStore, domainEvents, IncrementCounterOrProlongTimer);
            timer = new Timer(state => StopInternal().GetAwaiter().GetResult());
        }

        public string InputAddress { get; }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            transportInfrastructure = await transportCustomization.CreateTransportInfrastructure(InputAddress, transportSettings, Handle, faultManager.OnError, (_, __) => Task.CompletedTask, TransportTransactionMode.SendsAtomicWithReceive);
            messageReceiver = transportInfrastructure.Receivers[InputAddress];
            messageDispatcher = transportInfrastructure.Dispatcher;

            errorQueueTransportAddress = transportInfrastructure.ToTransportAddress(new QueueAddress(errorQueue));
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            timer.Dispose();
            endedPrematurely = true;
            await StopInternal();
            await transportInfrastructure.Shutdown(cancellationToken);
        }

        bool IsCounting => targetMessageCount.HasValue;

        async Task Handle(MessageContext message, CancellationToken cancellationToken)
        {
            if (Log.IsDebugEnabled)
            {
                var stagingId = message.Headers["ServiceControl.Retry.StagingId"];
                Log.DebugFormat("Handling message with id {0} and staging id {1} in input queue {2}", message.NativeMessageId, stagingId, InputAddress);
            }

            if (shouldProcess(message))
            {
                await returnToSender.HandleMessage(message, messageDispatcher, errorQueueTransportAddress, cancellationToken);
                IncrementCounterOrProlongTimer();
            }
            else
            {
                Log.WarnFormat("Rejecting message from staging queue as it's not part of a fully staged batch: {0}", message.NativeMessageId);
            }
        }

        void IncrementCounterOrProlongTimer()
        {
            if (IsCounting)
            {
                CountMessageAndStopIfReachedTarget();
            }
            else
            {
                if (Log.IsDebugEnabled)
                {
                    Log.Debug("Resetting timer");
                }

                timer.Change(TimeSpan.FromSeconds(45), Timeout.InfiniteTimeSpan);
            }
        }

        void CountMessageAndStopIfReachedTarget()
        {
            var currentMessageCount = Interlocked.Increment(ref actualMessageCount);
            if (Log.IsDebugEnabled)
            {
                Log.Debug($"Forwarding message {currentMessageCount} of {targetMessageCount}.");
            }

            if (currentMessageCount >= targetMessageCount.GetValueOrDefault())
            {
                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("Target count reached. Shutting down forwarder");
                }

                // NOTE: This needs to run on a different thread or a deadlock will happen trying to shut down the receiver
                _ = Task.Run(StopInternal);
            }
        }

        public virtual async Task Run(string forwardingBatchId, Predicate<MessageContext> filter, int? expectedMessageCount, CancellationToken cancellationToken = default)
        {
            CancellationTokenRegistration? registration = null;
            try
            {
                shouldProcess = filter;
                targetMessageCount = expectedMessageCount;
                actualMessageCount = 0;

                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("Starting receiver");
                }

                syncEvent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                stopCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                registration = cancellationToken.Register(() => _ = syncEvent.TrySetResult(true));

                await messageReceiver.StartReceive(cancellationToken);

                Log.Info($"Forwarder for batch {forwardingBatchId} started receiving messages from {messageReceiver.ReceiveAddress}.");

                if (!expectedMessageCount.HasValue)
                {
                    if (Log.IsDebugEnabled)
                    {
                        Log.Debug("Running in timeout mode. Starting timer.");
                    }

                    timer.Change(TimeSpan.FromSeconds(45), Timeout.InfiniteTimeSpan);
                }
            }
            finally
            {
                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat($"Waiting for forwarder for batch {forwardingBatchId} to finish.");
                }

                try
                {
                    Log.Debug("Awaiting syncEvent.Task.");
                    _ = await syncEvent.Task;
                    Log.Debug("Awaiting syncEvent.Task.");

                    Log.Debug("Disposing registration.");
                    registration?.Dispose();
                    Log.Debug("Registration disposed");

                    Log.Debug("Stopping message receiver.");
                    await messageReceiver.StopReceive(cancellationToken);
                    Log.Debug("Message receiver stopped.");
                }
                catch (Exception ex)
                {
                    Log.Error("An error occurred while stopping the transport receiver.", ex);
                    throw;
                }

                Log.Info($"Forwarder for batch {forwardingBatchId} finished forwarding all messages.");

                stopCompletionSource.TrySetResult(true);
            }

            if (endedPrematurely || cancellationToken.IsCancellationRequested)
            {
                throw new Exception("We are in the process of shutting down. Safe to ignore.");
            }
        }

        async Task StopInternal()
        {
            if (Log.IsDebugEnabled)
            {
                Log.Debug("Completing forwarding.");
            }

            syncEvent?.TrySetResult(true);
            await (stopCompletionSource?.Task ?? Task.CompletedTask);
            if (Log.IsDebugEnabled)
            {
                Log.Debug("Forwarding completed.");
            }
        }

        Timer timer;
        TaskCompletionSource<bool> syncEvent;
        TaskCompletionSource<bool> stopCompletionSource;
        bool endedPrematurely;
        int? targetMessageCount;
        int actualMessageCount;
        Predicate<MessageContext> shouldProcess;
        CaptureIfMessageSendingFails faultManager;
        ReturnToSender returnToSender;
        readonly string errorQueue;
        string errorQueueTransportAddress;
        readonly ITransportCustomization transportCustomization;
        readonly TransportSettings transportSettings;
        TransportInfrastructure transportInfrastructure;
        IMessageDispatcher messageDispatcher;
        IMessageReceiver messageReceiver;

        static readonly ILog Log = LogManager.GetLogger(typeof(ReturnToSenderDequeuer));

        class CaptureIfMessageSendingFails
        {
            public CaptureIfMessageSendingFails(IErrorMessageDataStore dataStore, IDomainEvents domainEvents, Action executeOnFailure)
            {
                this.dataStore = dataStore;
                this.executeOnFailure = executeOnFailure;
                this.domainEvents = domainEvents;
            }

            public async Task<ErrorHandleResult> OnError(ErrorContext errorContext, CancellationToken cancellationToken = default)
            {
                // We are currently not propagating the cancellation token further since it would require to change
                // the data store APIs and domain handlers to take a cancellation token. If this is needed it can be done
                // at a later time.
                _ = cancellationToken;

                try
                {
                    var message = errorContext.Message;
                    var destination = message.Headers["ServiceControl.TargetEndpointAddress"];
                    var messageUniqueId = message.Headers["ServiceControl.Retry.UniqueMessageId"];
                    Log.Warn($"Failed to send '{messageUniqueId}' message to '{destination}' for retry. Attempting to revert message status to unresolved so it can be tried again.", errorContext.Exception);

                    await dataStore.RevertRetry(messageUniqueId);

                    string reason;
                    try
                    {
                        reason = errorContext.Exception.GetBaseException().Message;
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

                return ErrorHandleResult.Handled;
            }

            readonly Action executeOnFailure;
            readonly IErrorMessageDataStore dataStore;
            readonly IDomainEvents domainEvents;
            static readonly ILog Log = LogManager.GetLogger(typeof(CaptureIfMessageSendingFails));
        }

    }
}