namespace ServiceControl.Audit.Auditing
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.Settings;
    using NServiceBus.Logging;

    class AuditIngestor
    {
        public AuditIngestor(AuditPersister auditPersister, IForwardMessages messageForwarder, Settings settings)
        {
            this.auditPersister = auditPersister;
            this.messageForwarder = messageForwarder;
            this.settings = settings;
        }

        public async Task Ingest(ProcessAuditMessageContext context)
        {
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Ingesting audit message {0}", context.Message.MessageId);
            }

            await auditPersister.Persist(context).ConfigureAwait(false);

            if (settings.ForwardAuditMessages)
            {
                await messageForwarder.Forward(context.Message, settings.AuditLogQueue)
                    .ConfigureAwait(false);
            }
        }

        public async Task Ingest(List<ProcessAuditMessageContext> contexts)
        {
            //if (log.IsDebugEnabled)
            //{
            //    log.DebugFormat("Ingesting audit message {0}", context.Message.MessageId);
            //}

            await auditPersister.Persist(contexts).ConfigureAwait(false);

            foreach (var context in contexts)
            {
                if (settings.ForwardAuditMessages)
                {
                    await messageForwarder.Forward(context.Message, settings.AuditLogQueue)
                        .ConfigureAwait(false);
                }

                context.Completed.TrySetResult(true);
            }
        }

        AuditPersister auditPersister;
        IForwardMessages messageForwarder;
        Settings settings;
        static ILog log = LogManager.GetLogger<AuditIngestor>();
    }
}