namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using Raven.Client;

    class FailedAuditImportCustomCheck : CustomCheck
    {
        public FailedAuditImportCustomCheck(IDocumentStore store)
            : base("Audit Message Ingestion", "ServiceControl Health", TimeSpan.FromHours(1))
        {
            this.store = store;
        }

        public override async Task<CheckResult> PerformCheck()
        {
            using (var session = store.OpenAsyncSession())
            {
                var query = session.Query<FailedAuditImport, FailedAuditImportIndex>();
                using (var ie = await session.Advanced.StreamAsync(query)
                    .ConfigureAwait(false))
                {
                    if (await ie.MoveNextAsync().ConfigureAwait(false))
                    {
                        Logger.Warn(message);
                        return CheckResult.Failed(message);
                    }
                }
            }

            return CheckResult.Pass;
        }

        readonly IDocumentStore store;

        const string message = @"One or more audit messages have failed to import properly into ServiceControl.Audit and have been stored in the ServiceControl.Audit database.
The import of these messages could have failed for a number of reasons and ServiceControl.Audit is not able to automatically reimport them. For guidance on how to resolve this see https://docs.particular.net/servicecontrol/import-failed-audit";

        static readonly ILog Logger = LogManager.GetLogger(typeof(FailedAuditImportCustomCheck));
    }
}