namespace ServiceControl.Hosting.Commands
{
    using System;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;

    static class AuditInstanceGuards
    {
        public static void EnsureCanRun(Settings settings)
        {
            var manifest = PersistenceManifestLibrary.Find(settings.PersistenceType);

            if (manifest?.SupportsAuditIngestion != true)
            {
                throw new Exception(
                    $"--audit-instance requires storage that supports audit ingestion, but this instance is configured to use '{settings.PersistenceType}'. "
                    + "A dedicated audit database is only supported on SQL Server and PostgreSQL storage.");
            }

            if (string.IsNullOrWhiteSpace(settings.ServiceControlQueueAddress))
            {
                throw new Exception(
                    "--audit-instance requires ServiceControl/ServiceControlQueueAddress, the primary instance's input queue, "
                    + "so that this host can report its custom checks and the endpoints it detects to the primary.");
            }

            if (settings.RemoteInstances.Length > 0)
            {
                throw new Exception(
                    "--audit-instance cannot have remote instances configured. An audit host is a leaf: the primary lists it under "
                    + "ServiceControl/RemoteInstances, not the other way round.");
            }
        }

        public static void EnsureNotCombinedWithIngestionOnly(bool auditInstance, bool ingestionOnly)
        {
            if (auditInstance && ingestionOnly)
            {
                throw new Exception(
                    "--audit-instance cannot be combined with --error-ingestion-only or --audit-ingestion-only. "
                    + "An audit host owns its database; a worker that scales its ingestion is started with --audit-ingestion-only alone.");
            }
        }
    }
}
