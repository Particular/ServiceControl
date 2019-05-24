namespace ServiceControl.Audit.Auditing
{
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Operations;
    using ServiceBus.Management.Infrastructure.Settings;

    class AuditIngestor
    {
        public AuditIngestor(AuditPersister auditPersister, IForwardMessages messageForwarder, Settings settings)
        {
            this.auditPersister = auditPersister;
            this.messageForwarder = messageForwarder;
            this.settings = settings;
        }

        public async Task Ingest(MessageContext context)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Ingesting audit message {0}", context.MessageId);
            }

            await auditPersister.Persist(context).ConfigureAwait(false);

            if (settings.ForwardAuditMessages)
            {
                await messageForwarder.Forward(context, settings.AuditLogQueue)
                    .ConfigureAwait(false);
            }
        }

        AuditPersister auditPersister;
        IForwardMessages messageForwarder;
        Settings settings;
        static ILog log = LogManager.GetLogger<AuditIngestor>();
    }
}
