﻿
namespace ServiceControl.Persistence.RavenDb.SagaAudit
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using ServiceControl.SagaAudit;
    using Raven.Client.Documents;

    // This custom check stays in the Raven3.5 persister becuase the Raven5 persister will not store audit data
    class AuditRetentionCustomCheck : CustomCheck
    {
        readonly IDocumentStore documentStore;
        readonly bool auditRetentionPeriodIsSet;

        public AuditRetentionCustomCheck(
            IDocumentStore documentStore,
            RavenDBPersisterSettings settings,
            TimeSpan? repeatAfter = null
            )
            : base("Saga Audit Data Retention", "ServiceControl Health", repeatAfter.HasValue ? repeatAfter : TimeSpan.FromHours(1))
        {
            this.documentStore = documentStore;
            auditRetentionPeriodIsSet = settings.AuditRetentionPeriod != default;
        }

        public override async Task<CheckResult> PerformCheck()
        {
            if (auditRetentionPeriodIsSet)
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
            using (var session = documentStore.OpenAsyncSession())
            {
                return await session.Query<SagaListIndex.Result, SagaListIndex>().AnyAsync();
            }
        }
    }
}