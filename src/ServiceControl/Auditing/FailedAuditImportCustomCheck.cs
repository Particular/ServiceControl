namespace ServiceControl.Operations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using CustomChecks.Internal;
    using NServiceBus.Logging;
    using Raven.Client;

    /// <summary>
    /// Legacy from the time the main instance handled also audits.
    /// </summary>
    class FailedAuditImportCustomCheck : CustomCheck
    {
        public FailedAuditImportCustomCheck(IDocumentStore store)
            : base("Audit Message Ingestion", "ServiceControl Health", TimeSpan.FromHours(1))
        {
            this.store = store;
        }

        public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            using (var session = store.OpenAsyncSession())
            {
                var query = session.Query<FailedAuditImport, FailedAuditImportIndex>();
                using (var ie = await session.Advanced.StreamAsync(query, cancellationToken)
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

        const string message = @"One or more audit messages have failed to import properly into ServiceControl and have been stored in the ServiceControl database.
The import of these messages could have failed for a number of reasons and ServiceControl is not able to automatically reimport them. For guidance on how to resolve this see https://docs.particular.net/servicecontrol/import-failed-messages";

        static readonly ILog Logger = LogManager.GetLogger(typeof(FailedAuditImportCustomCheck));
    }
}