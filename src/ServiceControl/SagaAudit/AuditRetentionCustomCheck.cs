namespace ServiceControl.SagaAudit
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using CustomChecks.Internal;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    class AuditRetentionCustomCheck : CustomCheck

    {
        readonly IDocumentStore _documentStore;
        readonly bool _auditRetentionPeriodIsSet;

        public AuditRetentionCustomCheck(IDocumentStore documentStore, Settings settings, TimeSpan? repeatAfter = null)
            : base("Saga Audit Data Retention", "ServiceControl Health", repeatAfter.HasValue ? repeatAfter : TimeSpan.FromHours(1))
        {
            _documentStore = documentStore;
            _auditRetentionPeriodIsSet = settings.AuditRetentionPeriod.HasValue;
        }

        public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            if (_auditRetentionPeriodIsSet)
            {
                return CheckResult.Pass;
            }

            if (await DetectSagaAuditData().ConfigureAwait(false))
            {
                return CheckResult.Failed("Saga snapshot data detected without an audit retention period configured. If saga audit data is allowed to accumulate, it can result in degraded performance.  Visit https://docs.particular.net/search?q=servicecontrol+troubleshooting for more information.");
            }

            return CheckResult.Pass;
        }

        async Task<bool> DetectSagaAuditData()
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                return await session.Query<SagaListIndex.Result, SagaListIndex>().AnyAsync().ConfigureAwait(false);
            }
        }
    }
}