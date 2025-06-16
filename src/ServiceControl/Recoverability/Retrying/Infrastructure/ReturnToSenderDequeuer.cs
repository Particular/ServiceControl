namespace ServiceControl.Recoverability;

using System;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.DomainEvents;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus;
using NServiceBus.Transport;
using Persistence;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Transports;

class ReturnToSenderDequeuer : IHostedService
{
    public ReturnToSenderDequeuer(
        ReturnToSender returnToSender,
        IErrorMessageDataStore dataStore,
        IDomainEvents domainEvents,
        ITransportCustomization transportCustomization,
        TransportSettings transportSettings,
        Settings settings,
        ErrorQueueNameCache errorQueueNameCache,
        ILogger<ReturnToSenderDequeuer> logger
    )
    {
        InputAddress = transportCustomization.ToTransportQualifiedQueueName(settings.StagingQueue);
        this.returnToSender = returnToSender;
        errorQueue = settings.ErrorQueue;
        this.transportCustomization = transportCustomization;
        this.transportSettings = transportSettings;
        this.errorQueueNameCache = errorQueueNameCache;
        this.logger = logger;
        faultManager = new CaptureIfMessageSendingFails(dataStore, domainEvents, IncrementCounterOrProlongTimer, logger);
        timer = new Timer(state => StopInternal().GetAwaiter().GetResult());
    }

    public string InputAddress { get; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        transportInfrastructure = await transportCustomization.CreateTransportInfrastructure(InputAddress, transportSettings, Handle, faultManager.OnError, (_, __) => Task.CompletedTask, TransportTransactionMode.SendsAtomicWithReceive);
        messageReceiver = transportInfrastructure.Receivers[InputAddress];
        messageDispatcher = transportInfrastructure.Dispatcher;

        errorQueueTransportAddress = transportInfrastructure.ToTransportAddress(new QueueAddress(errorQueue));
        errorQueueNameCache.ResolvedErrorAddress = errorQueueTransportAddress;
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
        var stagingId = message.Headers["ServiceControl.Retry.StagingId"];
        logger.LogDebug("Handling message with id {nativeMessageId} and staging id {stagingId} in input queue {inputAddress}", message.NativeMessageId, stagingId, InputAddress);

        if (shouldProcess(message))
        {
            await returnToSender.HandleMessage(message, messageDispatcher, errorQueueTransportAddress, cancellationToken);
            IncrementCounterOrProlongTimer();
        }
        else
        {
            logger.LogWarning("Rejecting message from staging queue as it's not part of a fully staged batch: {nativeMessageId}", message.NativeMessageId);
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
            logger.LogDebug("Resetting timer");

            timer.Change(TimeSpan.FromSeconds(45), Timeout.InfiniteTimeSpan);
        }
    }

    void CountMessageAndStopIfReachedTarget()
    {
        var currentMessageCount = Interlocked.Increment(ref actualMessageCount);

        logger.LogDebug("Forwarding message {currentMessageCount} of {targetMessageCount}", currentMessageCount, targetMessageCount);

        if (currentMessageCount >= targetMessageCount.GetValueOrDefault())
        {
            logger.LogDebug("Target count reached. Shutting down forwarder");

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

            logger.LogDebug("Starting receiver");

            syncEvent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            stopCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            registration = cancellationToken.Register(() => _ = syncEvent.TrySetResult(true));

            await messageReceiver.StartReceive(cancellationToken);

            logger.LogInformation("Forwarder for batch {forwardingBatchId} started receiving messages from {receiveAddress}", forwardingBatchId, messageReceiver.ReceiveAddress);

            if (!expectedMessageCount.HasValue)
            {
                logger.LogDebug("Running in timeout mode. Starting timer");

                timer.Change(TimeSpan.FromSeconds(45), Timeout.InfiniteTimeSpan);
            }
        }
        finally
        {
            logger.LogDebug("Waiting for forwarder for batch {forwardingBatchId} to finish", forwardingBatchId);

            await syncEvent.Task;
            registration?.Dispose();
            await messageReceiver.StopReceive(cancellationToken);

            logger.LogInformation("Forwarder for batch {forwardingBatchId} finished forwarding all messages", forwardingBatchId);

            stopCompletionSource.TrySetResult(true);
        }

        if (endedPrematurely || cancellationToken.IsCancellationRequested)
        {
            throw new Exception("We are in the process of shutting down. Safe to ignore.");
        }
    }

    async Task StopInternal()
    {
        logger.LogDebug("Completing forwarding");

        syncEvent?.TrySetResult(true);
        await (stopCompletionSource?.Task ?? Task.CompletedTask);

        logger.LogDebug("Forwarding completed");
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
    readonly ErrorQueueNameCache errorQueueNameCache;
    TransportInfrastructure transportInfrastructure;
    IMessageDispatcher messageDispatcher;
    IMessageReceiver messageReceiver;

    readonly ILogger<ReturnToSenderDequeuer> logger;

    class CaptureIfMessageSendingFails
    {
        public CaptureIfMessageSendingFails(IErrorMessageDataStore dataStore, IDomainEvents domainEvents, Action executeOnFailure, ILogger logger)
        {
            this.dataStore = dataStore;
            this.executeOnFailure = executeOnFailure;
            this.logger = logger;
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
                logger.LogWarning(errorContext.Exception, "Failed to send '{messageUniqueId}' message to '{destination}' for retry. Attempting to revert message status to unresolved so it can be tried again", messageUniqueId, destination);

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
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                // If something goes wrong here we just ignore, not the end of the world!
                logger.LogError(ex, "A failure occurred when trying to handle a retry failure");
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
        readonly ILogger logger;
    }
}
