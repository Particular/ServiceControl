namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    /// <summary>
    /// Runs a host that does nothing but drain the audit queue into the shared database, so several
    /// processes can ingest against one database. The host itself lands with the ingestion only
    /// composition; until then the command exists so the startup checks and the help text are in place,
    /// and it always fails because no shipped persister advertises audit support yet.
    /// </summary>
    class AuditIngestionOnlyCommand : AbstractCommand
    {
        public override Task Execute(HostArguments args, Settings settings, CancellationToken cancellationToken = default)
        {
            IngestionOnlyGuards.EnsureStorageSupportsAuditIngestion(settings);
            IngestionOnlyGuards.EnsureBodyStorageIsReadableByEveryHost("--audit-ingestion-only");

            throw new Exception(
                "--audit-ingestion-only is not available yet. The storage advertises audit support, but the audit ingestion only host has not been composed.");
        }
    }
}
