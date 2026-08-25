namespace ServiceControl.Auditing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NServiceBus.CustomChecks;
    using ServiceControl.Persistence;

    // Deliberately named and categorised differently from the standalone audit instance's check, which
    // reports into this same primary through ReportCustomChecksTo and would otherwise collide.
    class FailedAuditImportCustomCheck(IFailedAuditImportDataStore store, ILogger<FailedAuditImportCustomCheck> logger)
        : CustomCheck("Audit Message Ingestion (local)", "ServiceControl Health", TimeSpan.FromHours(1))
    {
        public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            if (await store.QueryContainsFailedImports(cancellationToken))
            {
                logger.LogWarning(message);
                return CheckResult.Failed(message);
            }

            return CheckResult.Pass;
        }

        const string message = @"One or more audit messages have failed to import properly into ServiceControl and have been stored in the ServiceControl database.
The import of these messages could have failed for a number of reasons and ServiceControl is not able to automatically reimport them. For guidance on how to resolve this see https://docs.particular.net/servicecontrol/import-failed-messages";
    }
}
