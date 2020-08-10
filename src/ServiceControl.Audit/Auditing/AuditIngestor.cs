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

    class AuditIngestor
    {
        public AuditIngestor(AuditPersister auditPersister, Settings settings)
        {
            this.auditPersister = auditPersister;
            this.settings = settings;
        }

        public async Task Ingest(List<MessageContext> contexts)
        {
            var stored = await auditPersister.Persist(contexts).ConfigureAwait(false);

            try
            {
                if (settings.ForwardAuditMessages)
                {
                    await Forward(stored, settings.AuditLogQueue)
                        .ConfigureAwait(false);
                }

                foreach (var context in stored)
                {
                    context.GetTaskCompletionSource().TrySetResult(true);
                }
            }
            catch (Exception e)
            {
                // in case forwarding throws
                foreach (var context in stored)
                {
                    context.GetTaskCompletionSource().TrySetException(e);
                }
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

        Task Forward(IReadOnlyCollection<MessageContext> messageContexts, string forwardingAddress)
        {
            var transportOperations = new TransportOperation[messageContexts.Count];
            var index = 0;
            MessageContext anyContext = null;
            foreach (var messageContext in messageContexts)
            {
                anyContext = messageContext;
                var outgoingMessage = new OutgoingMessage(
                    messageContext.MessageId,
                    messageContext.Headers,
                    messageContext.Body);

                // Forwarded messages should last as long as possible
                outgoingMessage.Headers.Remove(NServiceBus.Headers.TimeToBeReceived);

                transportOperations[index] = new TransportOperation(outgoingMessage, new UnicastAddressTag(forwardingAddress));
                index++;
            }

            return anyContext != null
                ? dispatcher.Dispatch(
                    new TransportOperations(transportOperations),
                    anyContext.TransportTransaction,
                    anyContext.Extensions
                )
                : Task.CompletedTask;
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