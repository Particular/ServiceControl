﻿namespace ServiceControl.Operations
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using ServiceBus.Management.Infrastructure.Settings;

    class ErrorIngestor
    {
        private static ILog log = LogManager.GetLogger<ErrorIngestor>();
        private IForwardMessages messageForwarder;
        private Settings settings;
        private FailedMessageAnnouncer failedMessageAnnouncer;
        private FailedMessagePersister failedMessagePersister;

        public ErrorIngestor(FailedMessagePersister failedMessagePersister, FailedMessageAnnouncer failedMessageAnnouncer, IForwardMessages messageForwarder, Settings settings)
        {
            this.failedMessagePersister = failedMessagePersister;
            this.failedMessageAnnouncer = failedMessageAnnouncer;
            this.messageForwarder = messageForwarder;
            this.settings = settings;
        }

        public async Task Ingest(MessageContext message)
        {
            message.Headers.TryGetValue(Headers.MessageId, out var originalMessageId);
            log.Debug($"Ingesting error message {message.MessageId} (original message id: {originalMessageId ?? string.Empty})");
            
            var failureDetails = await failedMessagePersister.Persist(message)
                .ConfigureAwait(false);
            
            await failedMessageAnnouncer.Announce(message.Headers, failureDetails)
                .ConfigureAwait(false);

            if (settings.ForwardErrorMessages)
            {
                await messageForwarder.Forward(message, settings.ErrorLogQueue)
                    .ConfigureAwait(false);
            }
        }
    }
}