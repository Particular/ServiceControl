namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Threading.Tasks;
    using Infrastructure;
    using NServiceBus.CustomChecks;

    class AuditIngestionCustomCheck : CustomCheck
    {
        public override Task<CheckResult> PerformCheck()
        {
            var failure = criticalErrorHolder.GetLastFailure();
            return failure == null
                ? successResult
                : Task.FromResult(CheckResult.Failed(failure));
        }

        public AuditIngestionCustomCheck(State criticalErrorHolder)
            : base("Audit Message Ingestion Process", CustomCheckCategories.ServiceControlAuditHealth, TimeSpan.FromSeconds(5))
        {
            this.criticalErrorHolder = criticalErrorHolder;
        }

        readonly State criticalErrorHolder;
        static Task<CheckResult> successResult = Task.FromResult(CheckResult.Pass);

        public class State
        {
            volatile string lastFailure;

            public void Clear() => lastFailure = null;
            public void ReportError(string failure) => lastFailure = failure;
            public string GetLastFailure()
            {
                return lastFailure;
            }
        }
    }

}