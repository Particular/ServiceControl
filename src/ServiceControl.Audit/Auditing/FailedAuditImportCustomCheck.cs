namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NServiceBus.CustomChecks;
    using ServiceControl.Audit.Persistence;

    class FailedAuditImportCustomCheck : CustomCheck
    {
        public FailedAuditImportCustomCheck(IFailedAuditStorage store, ILogger<FailedAuditImportCustomCheck> logger)
            : base("Audit Message Ingestion", "ServiceControl.Audit Health", TimeSpan.FromHours(1))
        {
            this.store = store;
            this.logger = logger;
        }

        public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            var count = await store.GetFailedAuditsCount();
            if (count > 0)
            {
                logger.LogWarning(message);
                return CheckResult.Failed(message);
            }

            return CheckResult.Pass;
        }

        readonly IFailedAuditStorage store;

        const string message = @"One or more audit messages have failed to import properly into ServiceControl.Audit and have been stored in the ServiceControl.Audit database.
The import of these messages could have failed for a number of reasons and ServiceControl.Audit is not able to automatically reimport them. For guidance on how to resolve this see https://docs.particular.net/servicecontrol/import-failed-messages";

        readonly ILogger<FailedAuditImportCustomCheck> logger;
    }
}