namespace ServiceControl.Audit.Auditing
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;


    class AuditImporter : IHostedService
    {
        readonly AuditIngestionComponent auditIngestion;
        readonly IMessageSession messageSession;

        public AuditImporter(AuditIngestionComponent auditIngestion, IMessageSession messageSession)
        {
            this.auditIngestion = auditIngestion;
            this.messageSession = messageSession;
        }

        public Task StartAsync(CancellationToken cancellationToken) => auditIngestion.Start(messageSession);

        public Task StopAsync(CancellationToken cancellationToken) => auditIngestion.Stop();
    }
}