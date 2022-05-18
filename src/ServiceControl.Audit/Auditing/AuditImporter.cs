namespace ServiceControl.Audit.Auditing
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;

    class AuditImporter : IHostedService
    {
        readonly AuditIngestionComponent auditIngestion;

        public AuditImporter(AuditIngestionComponent auditIngestion)
        {
            this.auditIngestion = auditIngestion;
        }

        public Task StartAsync(CancellationToken cancellationToken) => auditIngestion.Start();

        public Task StopAsync(CancellationToken cancellationToken) => auditIngestion.Stop();
    }
}