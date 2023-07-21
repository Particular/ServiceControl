
namespace ServiceControl.Persistence.RavenDb.SagaAudit
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using Persistence;
    using Persistence.RavenDb;
    using Raven.Client;
    using ServiceControl.SagaAudit;

    // This custom check stays in the Raven3.5 persister becuase the Raven5 persister will not store audit data
    class AuditRetentionCustomCheck : CustomCheck

    {
        readonly IDocumentStore _documentStore;
        readonly bool _auditRetentionPeriodIsSet;

        public AuditRetentionCustomCheck(IDocumentStore documentStore, PersistenceSettings settings, TimeSpan? repeatAfter = null)
            : base("Saga Audit Data Retention", "ServiceControl Health", repeatAfter.HasValue ? repeatAfter : TimeSpan.FromHours(1))
        {
            _documentStore = documentStore;
            _auditRetentionPeriodIsSet = settings.PersisterSpecificSettings.ContainsKey(RavenDbPersistenceConfiguration.AuditRetentionPeriodKey);
        }

        public override async Task<CheckResult> PerformCheck()
        {
            if (_auditRetentionPeriodIsSet)
            {
                return CheckResult.Pass;
            }

            if (await DetectSagaAuditData())
            {
                return CheckResult.Failed("Saga snapshot data detected without an audit retention period configured. If saga audit data is allowed to accumulate, it can result in degraded performance.  Visit https://docs.particular.net/search?q=servicecontrol+troubleshooting for more information.");
            }

            return CheckResult.Pass;
        }

        async Task<bool> DetectSagaAuditData()
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                return await session.Query<SagaListIndex.Result, SagaListIndex>().AnyAsync();
            }
        }
    }
}