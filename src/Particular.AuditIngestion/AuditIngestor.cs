
namespace Particular.AuditIngestion
{
    using System.Collections.Generic;
    using System.Linq;
    using Particular.AuditIngestion.Api;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Shell.Api.Ingestion;

    public class AuditIngestor : MessageIngestor
    {
        readonly IEnumerable<IProcessAuditMessages> auditMessageProcessors;

        public AuditIngestor(IEnumerable<IProcessAuditMessages> auditMessageProcessors)
        {
            this.auditMessageProcessors = auditMessageProcessors.ToArray();
        }

        public override string Address
        {
            get { return Settings.AuditQueueName; }
        }

        public override void Process(IngestedMessage message)
        {
            var auditMessage = new IngestedAuditMessage(message);

            foreach (var processor in auditMessageProcessors)
            {
                processor.Process(auditMessage);
            }
        }
    }
}
