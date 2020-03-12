namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure.Settings;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using Transports;

    class AuditIngestor
    {
        public AuditIngestor(AuditPersister auditPersister, Settings settings)
        {
            this.auditPersister = auditPersister;
            this.settings = settings;
        }

        public async Task Ingest(IReadOnlyCollection<BatchMessage> contexts)
        {
            //if (log.IsDebugEnabled)
            //{
            //    log.DebugFormat("Ingesting audit message {0}", context.Message.MessageId);
            //}

            await auditPersister.Persist(contexts).ConfigureAwait(false);

            // foreach (var context in contexts)
            // {
            //     if (settings.ForwardAuditMessages)
            //     {
            //         await Forward(message, settings.AuditLogQueue)
            //             .ConfigureAwait(false);
            //     }
            // }
        }

        public async Task Ingest(MessageContext message)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Ingesting audit message {0}", message.MessageId);
            }

            await auditPersister.Persist(message).ConfigureAwait(false);

            if (settings.ForwardAuditMessages)
            {
                await Forward(message, settings.AuditLogQueue)
                    .ConfigureAwait(false);
            }
        }

        public async Task Initialize(IDispatchMessages dispatcher)
        {
            this.dispatcher = dispatcher;
            if (settings.ForwardAuditMessages)
            {
                await VerifyCanReachForwardingAddress().ConfigureAwait(false);
            }
        }

        Task Forward(MessageContext messageContext, string forwardingAddress)
        {
            var outgoingMessage = new OutgoingMessage(
                messageContext.MessageId,
                messageContext.Headers,
                messageContext.Body);

            // Forwarded messages should last as long as possible
            outgoingMessage.Headers.Remove(NServiceBus.Headers.TimeToBeReceived);

            var transportOperations = new TransportOperations(
                new TransportOperation(outgoingMessage, new UnicastAddressTag(forwardingAddress))
            );

            return dispatcher.Dispatch(
                transportOperations,
                messageContext.TransportTransaction,
                messageContext.Extensions
            );
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
                        new UnicastAddressTag(settings.AuditLogQueue)
                    )
                );

                await dispatcher.Dispatch(transportOperations, new TransportTransaction(), new ContextBag())
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw new Exception($"Unable to write to forwarding queue {settings.AuditLogQueue}", e);
            }
        }

        AuditPersister auditPersister;
        IDispatchMessages dispatcher;
        Settings settings;
        static ILog log = LogManager.GetLogger<AuditIngestor>();
    }
}