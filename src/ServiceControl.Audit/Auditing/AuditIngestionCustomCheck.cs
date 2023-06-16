namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;

    class AuditIngestionCustomCheck : CustomCheck
    {
        public AuditIngestionCustomCheck(State criticalErrorHolder)
            : base("Audit Message Ingestion Process", "ServiceControl Health", TimeSpan.FromSeconds(5))
        {
            this.criticalErrorHolder = criticalErrorHolder;
        }

        public override Task<CheckResult> PerformCheck()
        {
            var failure = criticalErrorHolder.GetLastFailure();
            return failure == null
                ? successResult
                : Task.FromResult(CheckResult.Failed(failure));
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