namespace ServiceControl.SagaAudit
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;

    class AuditRetentionCheck : CustomCheck
    {
        public override Task<CheckResult> PerformCheck()
        {
            //Fake it until you make it
            return Task.FromResult(CheckResult.Failed("Audit retention is not set"));
        }

        public AuditRetentionCheck() : base("Saga Audit Message Ingestion", "ServiceControl Health", TimeSpan.FromHours(1))
        {
        }
    }
}
