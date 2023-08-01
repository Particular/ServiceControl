namespace NServiceBus.Transport.Msmq.DelayedDelivery
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Transactions;
    using Logging;
    using Extensibility;
    using Routing;

    class DelayedDeliveryPump : IPushMessages
    {
        public DelayedDeliveryPump(MsmqMessageDispatcher dispatcher,
                                   DueDelayedMessagePoller poller,
                                   IDelayedMessageStore storage,
                                   MessagePump messagePump,
                                   string timeoutsQueue,
                                   string errorQueue,
                                   int numberOfRetries,
                                   TimeSpan timeToWaitForStoreCircuitBreaker,
                                   Dictionary<string, string> faultMetadata)
        {
            this.dispatcher = dispatcher;
            this.poller = poller;
            this.storage = storage;
            this.numberOfRetries = numberOfRetries;
            this.timeToWaitForStoreCircuitBreaker = timeToWaitForStoreCircuitBreaker;
            this.faultMetadata = faultMetadata;
            pump = messagePump;
            this.errorQueue = errorQueue;
            this.timeoutsQueue = timeoutsQueue;
        }

        public Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            txOption = settings.RequiredTransactionMode == TransportTransactionMode.TransactionScope
                ? TransactionScopeOption.Required
                : TransactionScopeOption.RequiresNew;

            storeCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("DelayedDeliveryStore", timeToWaitForStoreCircuitBreaker, ex => criticalError.Raise("Failed to store delayed message", ex));
            poller.Init(criticalError, settings);

            // Make sure to use the timeouts input queue
            settings = new PushSettings(timeoutsQueue, settings.ErrorQueue, settings.PurgeOnStartup, settings.RequiredTransactionMode);

            return pump.Init(TimeoutReceived, OnError, criticalError, settings);
        }

        public void Start(PushRuntimeSettings limitations)
        {
            pump.Start(limitations);
            poller.Start();
        }

        public async Task Stop()
        {
            await pump.Stop().ConfigureAwait(false);
            await poller.Stop().ConfigureAwait(false);
        }

        async Task TimeoutReceived(MessageContext context)
        {
            if (!context.Headers.TryGetValue(MsmqUtilities.PropertyHeaderPrefix + MsmqMessageDispatcher.TimeoutDestination, out var destination))
            {
                throw new Exception("This message does not represent a timeout");
            }

            if (!context.Headers.TryGetValue(MsmqUtilities.PropertyHeaderPrefix + MsmqMessageDispatcher.TimeoutAt, out var atString))
            {
                throw new Exception("This message does not represent a timeout");
            }

            var id = context.MessageId; //Use message ID as a key in the delayed delivery table
            var at = DateTimeOffsetHelper.ToDateTimeOffset(atString);

            var message = context.Extensions.Get<System.Messaging.Message>();

            var diff = DateTime.UtcNow - at;

            if (diff.Ticks > 0) // Due
            {
                dispatcher.DispatchDelayedMessage(id, message.Extension, context.Body, destination, context.TransportTransaction, new ContextBag());
            }
            else
            {
                var timeout = new DelayedMessage
                {
                    Destination = destination,
                    MessageId = id,
                    Body = context.Body,
                    Time = at.UtcDateTime,
                    Headers = message.Extension
                };

                try
                {
                    using (var tx = new TransactionScope(txOption, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
                    {
                        await storage.Store(timeout).ConfigureAwait(false);
                        tx.Complete();
                    }

                    storeCircuitBreaker.Success();
                }
                catch (OperationCanceledException)
                {
                    //Shutting down
                    return;
                }
                catch (Exception e)
                {
                    await storeCircuitBreaker.Failure(e).ConfigureAwait(false);
                    throw new Exception("Error while storing delayed message", e);
                }

                poller.Signal(timeout.Time);
            }
        }

        async Task<ErrorHandleResult> OnError(ErrorContext errorContext)
        {
            Log.Error($"OnError {errorContext.Message.MessageId}", errorContext.Exception);

            if (errorContext.ImmediateProcessingFailures < numberOfRetries)
            {
                return ErrorHandleResult.RetryRequired;
            }

            var message = errorContext.Message;

            ExceptionHeaderHelper.SetExceptionHeaders(message.Headers, errorContext.Exception);

            foreach (var pair in faultMetadata)
            {
                message.Headers[pair.Key] = pair.Value;
            }

            var outgoingMessage = new OutgoingMessage(message.MessageId, message.Headers, message.Body);
            var transportOperation = new TransportOperation(outgoingMessage, new UnicastAddressTag(errorQueue));
            await dispatcher.Dispatch(new TransportOperations(transportOperation), errorContext.TransportTransaction, new ContextBag()).ConfigureAwait(false);

            return ErrorHandleResult.Handled;
        }

        readonly MsmqMessageDispatcher dispatcher;
        readonly DueDelayedMessagePoller poller;
        readonly IDelayedMessageStore storage;
        readonly int numberOfRetries;
        readonly TimeSpan timeToWaitForStoreCircuitBreaker;
        readonly MessagePump pump;
        readonly Dictionary<string, string> faultMetadata;
        readonly string errorQueue;
        readonly string timeoutsQueue;
        readonly TransactionOptions transactionOptions = new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted };

        RepeatedFailuresOverTimeCircuitBreaker storeCircuitBreaker;
        TransactionScopeOption txOption;

        static readonly ILog Log = LogManager.GetLogger<DelayedDeliveryPump>();
    }
}