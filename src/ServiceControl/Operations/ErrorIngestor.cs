namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Transport;

    class ErrorIngestor
    {
        public ErrorIngestor(ErrorPersister errorPersister, FailedMessageAnnouncer failedMessageAnnouncer, bool forwardErrorMessages, string errorLogQueue)
        {
            this.errorPersister = errorPersister;
            this.failedMessageAnnouncer = failedMessageAnnouncer;
            this.forwardErrorMessages = forwardErrorMessages;
            this.errorLogQueue = errorLogQueue;
        }

        public async Task Ingest(MessageContext message)
        {
            if (log.IsDebugEnabled)
            {
                message.Headers.TryGetValue(Headers.MessageId, out var originalMessageId);
                log.Debug($"Ingesting error message {message.MessageId} (original message id: {originalMessageId ?? string.Empty})");
            }

            var failureDetails = await errorPersister.Persist(message)
                .ConfigureAwait(false);

            await failedMessageAnnouncer.Announce(message.Headers, failureDetails)
                .ConfigureAwait(false);

            if (forwardErrorMessages)
            {
                await Forward(message).ConfigureAwait(false);
            }
        }

        Task Forward(MessageContext messageContext)
        {
            var outgoingMessage = new OutgoingMessage(
                messageContext.MessageId,
                messageContext.Headers,
                messageContext.Body);

            // Forwarded messages should last as long as possible
            outgoingMessage.Headers.Remove(Headers.TimeToBeReceived);

            var transportOperations = new TransportOperations(
                new TransportOperation(outgoingMessage, new UnicastAddressTag(errorLogQueue))
            );

            return dispatcher.Dispatch(
                transportOperations,
                messageContext.TransportTransaction,
                messageContext.Extensions
            );
        }

        public async Task Initialize(IDispatchMessages dispatcher)
        {
            this.dispatcher = dispatcher;
            if (forwardErrorMessages)
            {
                await VerifyCanReachForwardingAddress().ConfigureAwait(false);
            }
        }

        async Task VerifyCanReachForwardingAddress()
        {
            try
            {
                var transportOperations = new TransportOperations(
                    new TransportOperation(
                        new OutgoingMessage(Guid.Empty.ToString("N"),
                            new Dictionary<string, string>(),
                            new byte[0]),
                        new UnicastAddressTag(errorLogQueue)
                    )
                );

                await dispatcher.Dispatch(transportOperations, new TransportTransaction(), new ContextBag())
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw new Exception($"Unable to write to forwarding queue {errorLogQueue}", e);
            }
        }

        bool forwardErrorMessages;
        IDispatchMessages dispatcher;
        string errorLogQueue;
        FailedMessageAnnouncer failedMessageAnnouncer;
        ErrorPersister errorPersister;
        static ILog log = LogManager.GetLogger<ErrorIngestor>();
    }
}