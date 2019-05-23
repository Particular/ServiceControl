﻿namespace ServiceControl.Operations
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using ServiceBus.Management.Infrastructure.Settings;

    class ErrorIngestor
    {
        public ErrorIngestor(ErrorPersister errorPersister, FailedMessageAnnouncer failedMessageAnnouncer, IForwardMessages messageForwarder, Settings settings)
        {
            this.errorPersister = errorPersister;
            this.failedMessageAnnouncer = failedMessageAnnouncer;
            this.messageForwarder = messageForwarder;
            this.settings = settings;
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

            if (settings.ForwardErrorMessages)
            {
                await messageForwarder.Forward(message, settings.ErrorLogQueue)
                    .ConfigureAwait(false);
            }
        }

        IForwardMessages messageForwarder;
        Settings settings;
        FailedMessageAnnouncer failedMessageAnnouncer;
        ErrorPersister errorPersister;
        static ILog log = LogManager.GetLogger<ErrorIngestor>();
    }
}