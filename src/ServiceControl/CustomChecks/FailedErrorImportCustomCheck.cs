namespace ServiceControl.Operations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using Persistence;

    class FailedErrorImportCustomCheck : CustomCheck
    {
        public FailedErrorImportCustomCheck(IFailedErrorImportDataStore store)
            : base("Error Message Ingestion", "ServiceControl Health", TimeSpan.FromHours(1))
        {
            this.store = store;
        }

        public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            var hasFailedImports = await store.QueryContainsFailedImports();

            if (hasFailedImports)
            {
                Logger.Warn(Message);
                return CheckResult.Failed(Message);
            }

            return CheckResult.Pass;
        }

        readonly IFailedErrorImportDataStore store;

        const string Message = @"One or more error messages have failed to import properly into ServiceControl and have been stored in the ServiceControl database.
The import of these messages could have failed for a number of reasons and ServiceControl is not able to automatically reimport them. For guidance on how to resolve this see https://docs.particular.net/servicecontrol/import-failed-messages";

        static readonly ILog Logger = LogManager.GetLogger(typeof(FailedErrorImportCustomCheck));
    }
}