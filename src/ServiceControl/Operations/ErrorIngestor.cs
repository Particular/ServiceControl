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
    using ServiceBus.Management.Infrastructure.Settings;

    class ErrorIngestor
    {
        public ErrorIngestor(ErrorPersister errorPersister, FailedMessageAnnouncer failedMessageAnnouncer, Settings settings)
        {
            this.errorPersister = errorPersister;
            this.failedMessageAnnouncer = failedMessageAnnouncer;
            this.settings = settings;
        }

        public async Task Ingest(MessageContext message, IDispatchMessages dispatcher)
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

            if (settings.ForwardErrorMessages)
            {
                await Forward(message, settings.ErrorLogQueue, dispatcher)
                    .ConfigureAwait(false);
            }
        }

        static Task Forward(MessageContext messageContext, string forwardingAddress, IDispatchMessages dispatcher)
        {
            var outgoingMessage = new OutgoingMessage(
                messageContext.MessageId,
                messageContext.Headers,
                messageContext.Body);

            // Forwarded messages should last as long as possible
            outgoingMessage.Headers.Remove(Headers.TimeToBeReceived);

            var transportOperations = new TransportOperations(
                new TransportOperation(outgoingMessage, new UnicastAddressTag(forwardingAddress))
            );

            return dispatcher.Dispatch(
                transportOperations,
                messageContext.TransportTransaction,
                messageContext.Extensions
            );
        }

        public async Task VerifyCanReachForwardingAddress(string forwardingAddress, IDispatchMessages dispatcher)
        {
            try
            {
                var transportOperations = new TransportOperations(
                    new TransportOperation(
                        new OutgoingMessage(Guid.Empty.ToString("N"),
                            new Dictionary<string, string>(),
                            new byte[0]),
                        new UnicastAddressTag(forwardingAddress)
                    )
                );

                await dispatcher.Dispatch(transportOperations, new TransportTransaction(), new ContextBag())
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw new Exception($"Unable to write to forwarding queue {forwardingAddress}", e);
            }
        }

        Settings settings;
        FailedMessageAnnouncer failedMessageAnnouncer;
        ErrorPersister errorPersister;
        static ILog log = LogManager.GetLogger<ErrorIngestor>();
    }
}