namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using ServiceControl.Audit.Persistence;

    class FailedAuditImportCustomCheck : CustomCheck
    {
        public FailedAuditImportCustomCheck(IFailedAuditStorage store)
            : base("Audit Message Ingestion", "ServiceControl.Audit Health", TimeSpan.FromHours(1))
        {
            this.store = store;
        }

        public override async Task<CheckResult> PerformCheck()
        {
            var count = await store.GetFailedAuditsCount().ConfigureAwait(false);
            if (count > 0)
            {
                Logger.Warn(message);
                return CheckResult.Failed(message);
            }

            return CheckResult.Pass;
        }

        readonly IFailedAuditStorage store;

        const string message = @"One or more audit messages have failed to import properly into ServiceControl.Audit and have been stored in the ServiceControl.Audit database.
The import of these messages could have failed for a number of reasons and ServiceControl.Audit is not able to automatically reimport them. For guidance on how to resolve this see https://docs.particular.net/servicecontrol/import-failed-messages";

        static readonly ILog Logger = LogManager.GetLogger(typeof(FailedAuditImportCustomCheck));
    }
}